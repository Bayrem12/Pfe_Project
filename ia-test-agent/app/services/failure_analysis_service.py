"""
AI-powered failure analyzer.

Given a failed test step (Gherkin text + Playwright/executor error + selector +
generated code), produce a structured, human-readable diagnosis a la senior QA:

  • category        — one of: test | application | detection | timing | environment | unknown
  • root_cause      — short label: "ElementNotFound" / "AssertionMismatch" / ...
  • title           — one-line headline ("Login button not detected")
  • explanation     — multi-line reasoning ("Why did the test fail?")
  • where           — where in the pipeline the problem sits
  • is_test_issue   — True if scenario/step is wrong, False if app or infra is wrong
  • suggested_fix   — concrete actionable suggestion
  • confidence      — 0.0..1.0 — how certain the analyzer is about its verdict

The analyzer is fully deterministic (regex/keyword pattern-matching over the
error string).  It is *not* an LLM call — that keeps the agent self-contained,
offline-capable, and reproducible across runs while still giving the user
genuinely useful, structured failure analysis.
"""

from __future__ import annotations

import re
from dataclasses import dataclass, asdict
from typing import Optional


# ── Categories ───────────────────────────────────────────────────────────
CAT_TEST        = "test"          # scenario / step itself is wrong
CAT_APP         = "application"   # the application under test misbehaved
CAT_DETECTION   = "detection"     # vision/selector failed to locate the element
CAT_TIMING      = "timing"        # synchronization / timeout
CAT_ENVIRONMENT = "environment"   # network / target unreachable / dependency
CAT_UNKNOWN     = "unknown"


@dataclass
class FailureAnalysis:
    category: str = CAT_UNKNOWN
    root_cause: str = "UnknownFailure"
    title: str = "Test step failed"
    explanation: str = ""
    where: str = ""
    is_test_issue: bool = False
    suggested_fix: str = ""
    confidence: float = 0.5
    analysis_method: str = "regex"  # regex | semantic-ai | zero-shot-ai | llm-ai | fallback

    def to_dict(self) -> dict:
        return asdict(self)


# ── Pattern → analysis rules ─────────────────────────────────────────────
# Each rule: (regex applied case-insensitively to the error string, builder fn).
# The first matching rule wins.

def _truncate(text: str, n: int = 220) -> str:
    text = (text or "").strip().replace("\r", " ")
    if len(text) <= n:
        return text
    return text[: n - 1] + "…"


def _navigation_failure(err: str, step: str, **_) -> FailureAnalysis:
    return FailureAnalysis(
        category=CAT_ENVIRONMENT,
        root_cause="NavigationFailure",
        title="The application URL could not be reached",
        explanation=(
            "The browser failed to load the target page. This usually means "
            "the URL is wrong, the server is down, the user is offline, or "
            "DNS resolution failed.\n"
            f"Reported by Playwright: {_truncate(err)}"
        ),
        where="Browser → target application (network/DNS layer)",
        is_test_issue=False,
        suggested_fix=(
            "1) Confirm the URL in the Given step is reachable from the "
            "machine running the agent (open it manually).\n"
            "2) Verify the application is started and listening on that port.\n"
            "3) If using Docker, check container networking and host name."
        ),
        confidence=0.9,
    )


def _timeout_failure(err: str, step: str, **_) -> FailureAnalysis:
    return FailureAnalysis(
        category=CAT_TIMING,
        root_cause="WaitTimeoutExceeded",
        title="The expected element never became actionable in time",
        explanation=(
            "Playwright waited for an element to appear / become visible / "
            "become enabled, but the wait expired. The element may render "
            "after a slow API call, behind a loading spinner, or only after "
            "a previous step completes asynchronously.\n"
            f"Original Playwright error: {_truncate(err)}"
        ),
        where="Playwright wait → DOM (synchronization)",
        is_test_issue=False,
        suggested_fix=(
            "• Add an explicit wait for the loading indicator to disappear "
            "before the failing step.\n"
            "• Increase the action timeout for slow back-ends.\n"
            "• Check that the previous step actually triggered the navigation/"
            "request expected by this step."
        ),
        confidence=0.85,
    )


def _element_not_found(err: str, step: str, selector: str = "", **_) -> FailureAnalysis:
    sel = selector or "(no selector recorded)"
    return FailureAnalysis(
        category=CAT_DETECTION,
        root_cause="ElementNotFound",
        title="The target UI element could not be located",
        explanation=(
            f"The action targeted the element described by the step "
            f"\"{_truncate(step, 120)}\" using the selector `{sel}`, "
            "but no node matched it on the live page. Either the selector is "
            "outdated (UI changed), the element is rendered inside an iframe / "
            "shadow DOM, or the visual fallback (YOLO + OCR) couldn't recognise "
            "it on the screenshot.\n"
            f"Engine error: {_truncate(err)}"
        ),
        where="Vision/Selector layer → DOM",
        is_test_issue=False,
        suggested_fix=(
            "• Open the page manually and inspect the element — does it still "
            "exist with the same text/role?\n"
            "• Define a project Action Mapping that pins this step to a stable "
            "selector (e.g. data-testid).\n"
            "• If the element is inside an iframe, expose it via a frame locator."
        ),
        confidence=0.88,
    )


def _strict_mode_violation(err: str, step: str, **_) -> FailureAnalysis:
    return FailureAnalysis(
        category=CAT_DETECTION,
        root_cause="AmbiguousSelector",
        title="The selector matches more than one element",
        explanation=(
            "Playwright's strict mode rejected the action because several "
            "nodes match the locator. This makes the step non-deterministic — "
            "the engine refuses to guess which one to interact with.\n"
            f"Reported: {_truncate(err)}"
        ),
        where="Selector layer (Playwright strict mode)",
        is_test_issue=True,
        suggested_fix=(
            "• Refine the step text to be more specific (e.g. 'click the "
            "Submit button in the login form').\n"
            "• Add an Action Mapping with a precise selector "
            "(data-testid, role + name, or .nth())."
        ),
        confidence=0.9,
    )


def _assertion_failed(err: str, step: str, **_) -> FailureAnalysis:
    return FailureAnalysis(
        category=CAT_APP,
        root_cause="AssertionMismatch",
        title="The application did not behave as the scenario expected",
        explanation=(
            "The 'Then' assertion compared an actual UI value/state to the "
            "expected one and they didn't match. This usually points at a "
            "real bug or regression in the application itself — not at the "
            "test framework.\n"
            f"Diff: {_truncate(err)}"
        ),
        where="Application under test (functional behaviour)",
        is_test_issue=False,
        suggested_fix=(
            "• Reproduce the scenario manually and compare with the expected "
            "result — if the app shows the wrong value, file a bug.\n"
            "• If the expected value in the scenario is outdated, update the "
            "Then step to the new business rule."
        ),
        confidence=0.85,
    )


# ── Precision rules for the assertions our generator emits ────────────────
# These match the EXACT error strings produced by generator_service.py and
# extract the expected/actual values to give a much more concrete verdict.

_DROPDOWN_RE = re.compile(
    r"Dropdown mismatch:\s*expected\s*['\"]?([^'\"]+?)['\"]?\s+but\s+different\s+option\s+selected",
    re.I,
)


def _dropdown_mismatch(err: str, step: str, **_) -> FailureAnalysis:
    m = _DROPDOWN_RE.search(err)
    expected = m.group(1).strip() if m else "the expected option"
    return FailureAnalysis(
        category=CAT_APP,
        root_cause="DropdownValueMismatch",
        title=f"Dropdown shows a different option than '{expected}'",
        explanation=(
            f"The 'Then' step asserted that the <select> dropdown should be "
            f"showing '{expected}', but the option currently selected on the "
            f"page is different. This is a real functional discrepancy — "
            f"either the previous 'When' step picked the wrong option, or the "
            f"scenario expects an option that doesn't reflect the action that "
            f"was just performed.\n"
            f"Engine error: {_truncate(err)}"
        ),
        where="Application under test → <select> element",
        is_test_issue=False,
        suggested_fix=(
            f"• Re-run the scenario in headed mode and watch which option the "
            f"'When … select …' step actually picks.\n"
            f"• If the previous step selected '{expected}', the dropdown is "
            f"reverting after selection — file a UI bug.\n"
            f"• If the scenario expected the wrong label, update the Then step "
            f"to match the option chosen in the When step (case and spaces "
            f"must match exactly: '{expected}')."
        ),
        confidence=0.95,
    )


_LOGIN_URL_RE = re.compile(
    r"Login failed.{0,5}still on auth page:\s*(\S+)", re.I,
)


def _login_still_on_auth(err: str, step: str, **_) -> FailureAnalysis:
    m = _LOGIN_URL_RE.search(err)
    url = m.group(1) if m else "the login page"
    return FailureAnalysis(
        category=CAT_APP,
        root_cause="LoginRedirectFailed",
        title="Login did not redirect — still on the auth page",
        explanation=(
            "The scenario expects the user to be authenticated, but after the "
            "submit the browser is still on the login URL. The credentials "
            "were almost certainly rejected, or the form did not submit at "
            "all (validation error, disabled button, missing field).\n"
            f"Current URL: {url}\n"
            f"Engine error: {_truncate(err)}"
        ),
        where="Application under test → authentication flow",
        is_test_issue=False,
        suggested_fix=(
            "• Verify the credentials in the When steps are valid for the "
            "target environment (use quoted values: 'tomsmith' / 'SuperSecretPassword!').\n"
            "• Open the page in headed mode — is the submit button enabled?\n"
            "• If the credentials are correct, the auth backend may be down "
            "or rejecting valid users — file a bug."
        ),
        confidence=0.95,
    )


def _login_error_visible(err: str, step: str, **_) -> FailureAnalysis:
    return FailureAnalysis(
        category=CAT_APP,
        root_cause="LoginErrorMessageShown",
        title="Login form is showing an error message",
        explanation=(
            "After submitting the credentials, an error message such as "
            "\"invalid\", \"incorrect password\", or \"login failed\" became "
            "visible on the page. This means the application explicitly "
            "rejected the login attempt.\n"
            f"Engine error: {_truncate(err)}"
        ),
        where="Application under test → authentication response",
        is_test_issue=False,
        suggested_fix=(
            "• Confirm the credentials in the scenario match a valid account.\n"
            "• If the password was recently changed, update the scenario's "
            "quoted value.\n"
            "• Check the screenshot for the exact error message displayed by "
            "the app."
        ),
        confidence=0.95,
    )


_VISIBLE_TEXT_RE = re.compile(
    r"Expected visible text\s*['\"]([^'\"]+)['\"]\s*not found", re.I,
)


def _text_not_visible(err: str, step: str, **_) -> FailureAnalysis:
    m = _VISIBLE_TEXT_RE.search(err)
    expected = m.group(1).strip() if m else "the expected text"
    return FailureAnalysis(
        category=CAT_APP,
        root_cause="ExpectedTextNotVisible",
        title=f"The text '{expected}' is not visible on the page",
        explanation=(
            f"The 'Then' step expected the text '{expected}' to be visible to "
            f"the user, but a strict scan of the live DOM (excluding hidden "
            f"nodes, <option>, <script>, aria-hidden, display:none) did not "
            f"find it. Either the text is wrong, the page rendered a different "
            f"state, or the element is hidden behind an animation/modal.\n"
            f"Engine error: {_truncate(err)}"
        ),
        where="Application under test → rendered DOM",
        is_test_issue=False,
        suggested_fix=(
            f"• Open the page manually after the previous step — does "
            f"'{expected}' appear with the same casing and spacing?\n"
            f"• If the wording changed, update the Then step.\n"
            f"• If it should appear but doesn't, the previous action did not "
            f"trigger the expected UI state — investigate the When step."
        ),
        confidence=0.92,
    )


_ROW_COUNT_RE = re.compile(r"Expected\s+(\d+)\s+rows,\s*got\s+(\d+)", re.I)


def _row_count_mismatch(err: str, step: str, **_) -> FailureAnalysis:
    m = _ROW_COUNT_RE.search(err)
    if m:
        expected, actual = m.group(1), m.group(2)
        title = f"Table has {actual} rows but {expected} were expected"
        explanation = (
            f"The scenario expected the table/list to contain {expected} rows, "
            f"but the live DOM currently shows {actual}. This is a real data "
            f"discrepancy — the back-end returned a different number of items "
            f"than the scenario assumes."
        )
    else:
        title = "Row count does not match the expected value"
        explanation = (
            "The scenario expected a specific number of rows but found a "
            "different count.\n"
            f"Engine error: {_truncate(err)}"
        )
    return FailureAnalysis(
        category=CAT_APP,
        root_cause="RowCountMismatch",
        title=title,
        explanation=explanation,
        where="Application under test → data layer",
        is_test_issue=False,
        suggested_fix=(
            "• Check the test data state — was a fixture missing or duplicated?\n"
            "• If pagination is involved, verify the table is on the right page.\n"
            "• If the expected count is outdated, update the Then step."
        ),
        confidence=0.93,
    )


def _intercepted_click(err: str, step: str, **_) -> FailureAnalysis:
    return FailureAnalysis(
        category=CAT_APP,
        root_cause="ClickIntercepted",
        title="A different element captured the click",
        explanation=(
            "Playwright tried to click the target but another element (modal, "
            "tooltip, cookie banner, overlay) was on top of it at the moment "
            "of the click. The action was intercepted by that overlay.\n"
            f"Detail: {_truncate(err)}"
        ),
        where="Application under test (UI layout / z-index)",
        is_test_issue=False,
        suggested_fix=(
            "• Add a step that dismisses the blocking overlay (cookie banner, "
            "modal) before this one.\n"
            "• If the overlay is unintended, file a UI bug — clicks on visible "
            "elements should not be hijacked."
        ),
        confidence=0.92,
    )


def _detached_element(err: str, step: str, **_) -> FailureAnalysis:
    return FailureAnalysis(
        category=CAT_TIMING,
        root_cause="ElementDetached",
        title="The element was removed from the DOM mid-action",
        explanation=(
            "The element was matched but disappeared from the page (re-render / "
            "route change) before the action could complete. This is a timing "
            "issue, often caused by an asynchronous re-render after a previous "
            "step.\n"
            f"Detail: {_truncate(err)}"
        ),
        where="DOM lifecycle (synchronization)",
        is_test_issue=False,
        suggested_fix=(
            "• Wait for the previous network request / animation to finish "
            "before locating the element.\n"
            "• Re-locate the element just before the action (avoid stored "
            "locator references across re-renders)."
        ),
        confidence=0.85,
    )


def _invalid_step(err: str, step: str, **_) -> FailureAnalysis:
    return FailureAnalysis(
        category=CAT_TEST,
        root_cause="InvalidScenarioStep",
        title="The Gherkin step is malformed or unsupported",
        explanation=(
            "The NLP / generator could not turn this step into a valid action. "
            "It might be missing a target, mixing two intents, or using a verb "
            "the agent does not support.\n"
            f"Step text: \"{_truncate(step, 160)}\"\n"
            f"Engine error: {_truncate(err)}"
        ),
        where="Scenario → NLP/Generator layer",
        is_test_issue=True,
        suggested_fix=(
            "• Rewrite the step using the supported pattern, e.g. "
            "'When I click the Login button' or 'Then I should see \"Welcome\"'.\n"
            "• Split compound steps (and / then) into separate Gherkin lines."
        ),
        confidence=0.8,
    )


# Ordered list of (regex, builder).  More specific patterns must come first.
_RULES: list[tuple[re.Pattern, callable]] = [
    # — High-precision rules for OUR generator's exact assertion strings —
    (_DROPDOWN_RE,                                                                                                 _dropdown_mismatch),
    (_LOGIN_URL_RE,                                                                                                _login_still_on_auth),
    (re.compile(r"Login failed.{0,5}error message visible", re.I),                                                _login_error_visible),
    (_VISIBLE_TEXT_RE,                                                                                             _text_not_visible),
    (_ROW_COUNT_RE,                                                                                                _row_count_mismatch),

    # — Network / navigation —
    (re.compile(r"net::ERR_|ERR_NAME_NOT_RESOLVED|ERR_CONNECTION_REFUSED|ECONNREFUSED|net::ERR_INTERNET_DISCONNECTED", re.I), _navigation_failure),
    (re.compile(r"page\.goto|navigation (failed|timeout|to \"https?:)", re.I), _navigation_failure),

    # — Strict mode (must beat plain "not found") —
    (re.compile(r"strict mode violation|resolved to \d+ elements", re.I), _strict_mode_violation),

    # — Click intercepted / overlay —
    (re.compile(r"intercept(s|ed) (the )?(pointer|click)|element (is )?not stable|covered by another element", re.I), _intercepted_click),

    # — Detached —
    (re.compile(r"element is not attached|node is detached|element was detached|target closed", re.I), _detached_element),

    # — Timeout —
    (re.compile(r"timeout|timed out|exceeded \d+ ?ms|waiting for", re.I), _timeout_failure),

    # — Element not found —
    (re.compile(r"locator\.[\w]+|no node found|no element|element not (found|visible)|cannot find element|fallback failed|aucun candidat|element introuvable", re.I), _element_not_found),

    # — Generic assertion (broadened: catches "mismatch", "differ", standalone "expected") —
    (re.compile(
        r"expect\(|to (have|be|contain)|assertion|"
        r"expected .* (received|but|got|actual)|"
        r"\bmismatch\b|\bdiffer(s|ed)?\b|"
        r"attendu|valeur attendue",
        re.I,
    ), _assertion_failed),

    # — Malformed step (NLP / generator complaints) —
    (re.compile(r"unsupported step|cannot parse|nlp (failed|could not)|step ignored|unknown intent|impossible d['']analyser", re.I), _invalid_step),
]


# ── ML-powered analysis stages (Stages 2–4) ──────────────────────────────
import logging as _logging  # noqa: E402
_log = _logging.getLogger(__name__)

# ── Ollama availability ───────────────────────────────────────────────────
try:
    import ollama as _ollama  # type: ignore
    _FA_OLLAMA_AVAILABLE = True
except ImportError:
    _FA_OLLAMA_AVAILABLE = False

# ── Device selection (GPU if available, CPU otherwise) ───────────────────
def _cuda_device() -> int:
    """Return 0 (first GPU) when CUDA is available, -1 (CPU fallback)."""
    try:
        import torch
        if torch.cuda.is_available():
            _log.info(
                "CUDA available – ML models will use GPU (%s)",
                torch.cuda.get_device_name(0),
            )
            return 0
    except Exception:
        pass
    return -1


# ── SBERT singleton (lazy-loaded) ─────────────────────────────────────────
_FA_SBERT_INSTANCE = None


def _fa_embedding_model():
    global _FA_SBERT_INSTANCE
    if _FA_SBERT_INSTANCE is None:
        try:
            from sentence_transformers import SentenceTransformer
            from app.config import settings
            _FA_SBERT_INSTANCE = SentenceTransformer(
                settings.EMBEDDING_MODEL,
                local_files_only=True,
                device=_cuda_device(),
            )
        except Exception as exc:
            _log.warning("SBERT not available for failure analysis: %s", exc)
    return _FA_SBERT_INSTANCE


# ── Semantic knowledge base ───────────────────────────────────────────────
_KNOWLEDGE_BASE: list[dict] = [
    # --- Timing / Timeout ---
    {"description": "timeout waiting for element locator to be visible or actionable",
     "category": CAT_TIMING, "root_cause": "WaitTimeoutExceeded",
     "title": "Element never became actionable in time", "is_test_issue": False, "confidence": 0.82},
    {"description": "page load timeout exceeded navigation did not complete",
     "category": CAT_TIMING, "root_cause": "NavigationTimeout",
     "title": "Page navigation timed out", "is_test_issue": False, "confidence": 0.82},
    {"description": "waiting for network idle request still pending",
     "category": CAT_TIMING, "root_cause": "NetworkIdle",
     "title": "Page is still waiting for network requests", "is_test_issue": False, "confidence": 0.78},
    # --- Navigation / Environment ---
    {"description": "failed to navigate to URL connection refused ECONNREFUSED",
     "category": CAT_ENVIRONMENT, "root_cause": "NavigationFailure",
     "title": "Application URL could not be reached", "is_test_issue": False, "confidence": 0.88},
    {"description": "DNS resolution failed hostname not found network error",
     "category": CAT_ENVIRONMENT, "root_cause": "DNSFailure",
     "title": "DNS resolution failed for the target host", "is_test_issue": False, "confidence": 0.88},
    {"description": "server returned 500 internal server error bad gateway 503",
     "category": CAT_ENVIRONMENT, "root_cause": "ServerError",
     "title": "The server returned an error response", "is_test_issue": False, "confidence": 0.85},
    {"description": "404 not found page resource missing HTTP error response",
     "category": CAT_ENVIRONMENT, "root_cause": "ResourceNotFound",
     "title": "Server returned 404 Not Found", "is_test_issue": False, "confidence": 0.85},
    {"description": "CORS cross-origin request blocked policy API call rejected",
     "category": CAT_ENVIRONMENT, "root_cause": "CORSError",
     "title": "Cross-origin request blocked by CORS policy", "is_test_issue": False, "confidence": 0.82},
    # --- Element not found / Detection ---
    {"description": "element not found no node matched locator selector",
     "category": CAT_DETECTION, "root_cause": "ElementNotFound",
     "title": "Target UI element could not be located", "is_test_issue": False, "confidence": 0.85},
    {"description": "button link not found in DOM page element missing",
     "category": CAT_DETECTION, "root_cause": "ElementNotFound",
     "title": "Expected button or link not present on page", "is_test_issue": False, "confidence": 0.82},
    {"description": "image icon not detected visual fallback YOLO OCR failed screenshot",
     "category": CAT_DETECTION, "root_cause": "VisualFallbackFailed",
     "title": "Visual detection (AI) could not locate the element", "is_test_issue": False, "confidence": 0.80},
    {"description": "element inside iframe shadow DOM not reachable",
     "category": CAT_DETECTION, "root_cause": "IframeOrShadowDOM",
     "title": "Element inside iframe or shadow DOM not accessible", "is_test_issue": False, "confidence": 0.78},
    {"description": "scroll page element not in viewport cannot interact",
     "category": CAT_DETECTION, "root_cause": "ElementOutOfViewport",
     "title": "Element is outside the visible viewport", "is_test_issue": False, "confidence": 0.75},
    {"description": "button not clickable not enabled element not interactable",
     "category": CAT_DETECTION, "root_cause": "ElementNotInteractable",
     "title": "Element exists but is not interactable", "is_test_issue": False, "confidence": 0.78},
    # --- Strict mode / Ambiguous ---
    {"description": "strict mode violation multiple elements match selector ambiguous locator",
     "category": CAT_DETECTION, "root_cause": "AmbiguousSelector",
     "title": "Selector matches multiple elements", "is_test_issue": True, "confidence": 0.88},
    # --- Assertion / App ---
    {"description": "assertion failed expected value received actual mismatch differ",
     "category": CAT_APP, "root_cause": "AssertionMismatch",
     "title": "Assertion failed — expected vs actual mismatch", "is_test_issue": False, "confidence": 0.82},
    {"description": "text not visible on page expected content missing DOM",
     "category": CAT_APP, "root_cause": "ExpectedTextNotVisible",
     "title": "Expected text not visible on the page", "is_test_issue": False, "confidence": 0.82},
    {"description": "login failed wrong password invalid credentials authentication error",
     "category": CAT_APP, "root_cause": "AuthenticationFailed",
     "title": "Login failed — invalid credentials", "is_test_issue": False, "confidence": 0.88},
    {"description": "login form still showing after submit not redirected auth page",
     "category": CAT_APP, "root_cause": "LoginRedirectFailed",
     "title": "Login did not redirect after submit", "is_test_issue": False, "confidence": 0.87},
    {"description": "dropdown wrong option selected value mismatch select element",
     "category": CAT_APP, "root_cause": "DropdownValueMismatch",
     "title": "Dropdown shows wrong option", "is_test_issue": False, "confidence": 0.85},
    {"description": "table row count does not match expected number of rows",
     "category": CAT_APP, "root_cause": "RowCountMismatch",
     "title": "Table row count does not match expected", "is_test_issue": False, "confidence": 0.85},
    {"description": "form validation error required field missing input rejected",
     "category": CAT_APP, "root_cause": "FormValidationError",
     "title": "Form rejected due to validation error", "is_test_issue": False, "confidence": 0.80},
    {"description": "modal dialog not showing expected popup not appeared",
     "category": CAT_APP, "root_cause": "ModalNotShown",
     "title": "Expected modal or dialog did not appear", "is_test_issue": False, "confidence": 0.78},
    {"description": "redirect to wrong page unexpected URL after action",
     "category": CAT_APP, "root_cause": "UnexpectedRedirect",
     "title": "Unexpected page redirect after action", "is_test_issue": False, "confidence": 0.80},
    {"description": "click intercepted blocked by overlay modal cookie banner on top",
     "category": CAT_APP, "root_cause": "ClickIntercepted",
     "title": "Click blocked by overlay element", "is_test_issue": False, "confidence": 0.87},
    {"description": "checkbox not checked state incorrect toggle state wrong",
     "category": CAT_APP, "root_cause": "CheckboxStateMismatch",
     "title": "Checkbox state does not match expected", "is_test_issue": False, "confidence": 0.78},
    {"description": "file upload failed attachment not uploaded input file",
     "category": CAT_APP, "root_cause": "FileUploadFailed",
     "title": "File upload failed or attachment not found", "is_test_issue": False, "confidence": 0.78},
    {"description": "alert dialog browser prompt not handled popup",
     "category": CAT_APP, "root_cause": "UnhandledAlert",
     "title": "Browser alert or dialog was not handled", "is_test_issue": False, "confidence": 0.78},
    {"description": "JavaScript error console exception thrown runtime error",
     "category": CAT_APP, "root_cause": "JavaScriptError",
     "title": "JavaScript runtime error on the page", "is_test_issue": False, "confidence": 0.78},
    {"description": "element disabled cannot click interact with disabled button input",
     "category": CAT_APP, "root_cause": "ElementDisabled",
     "title": "Element is disabled and cannot be interacted with", "is_test_issue": False, "confidence": 0.82},
    {"description": "input text field value wrong expected value not entered correctly",
     "category": CAT_APP, "root_cause": "InputValueMismatch",
     "title": "Input field value does not match expected", "is_test_issue": False, "confidence": 0.75},
    {"description": "API request returned error status backend response failure",
     "category": CAT_APP, "root_cause": "APICallFailed",
     "title": "Backend API call returned an error", "is_test_issue": False, "confidence": 0.78},
    {"description": "page title heading wrong incorrect content displayed",
     "category": CAT_APP, "root_cause": "WrongPageContent",
     "title": "Page displays wrong content", "is_test_issue": False, "confidence": 0.75},
    # --- Timing ---
    {"description": "element detached from DOM removed re-render route change",
     "category": CAT_TIMING, "root_cause": "ElementDetached",
     "title": "Element removed from DOM mid-action", "is_test_issue": False, "confidence": 0.84},
    # --- Test scenario issues ---
    {"description": "unsupported step invalid gherkin cannot parse NLP failed",
     "category": CAT_TEST, "root_cause": "InvalidScenarioStep",
     "title": "Gherkin step is malformed or unsupported", "is_test_issue": True, "confidence": 0.80},
    {"description": "missing precondition test data not set up fixture absent",
     "category": CAT_TEST, "root_cause": "MissingPrecondition",
     "title": "Required test precondition is missing", "is_test_issue": True, "confidence": 0.75},
    {"description": "test teardown setup failed before after hook error",
     "category": CAT_TEST, "root_cause": "HookFailure",
     "title": "Test setup or teardown hook failed", "is_test_issue": True, "confidence": 0.75},
    {"description": "tab window switched unexpectedly wrong browser context",
     "category": CAT_TEST, "root_cause": "WrongBrowserContext",
     "title": "Browser context is on the wrong tab or window", "is_test_issue": True, "confidence": 0.72},
]

_FA_KB_EMBEDDINGS = None  # numpy array cached after first call


def _fa_kb_embeddings():
    """Lazy-compute and cache embeddings for the knowledge base."""
    global _FA_KB_EMBEDDINGS
    if _FA_KB_EMBEDDINGS is not None:
        return _FA_KB_EMBEDDINGS
    model = _fa_embedding_model()
    if model is None:
        return None
    try:
        import numpy as np
        descs = [e["description"] for e in _KNOWLEDGE_BASE]
        embs = model.encode(descs, normalize_embeddings=True, show_progress_bar=False)
        _FA_KB_EMBEDDINGS = np.array(embs, dtype="float32")
    except Exception as exc:
        _log.warning("Failed to encode KB embeddings: %s", exc)
        return None
    return _FA_KB_EMBEDDINGS


def _semantic_match(err: str, step: str, selector: str = "") -> "Optional[FailureAnalysis]":
    """Stage 2: SBERT cosine similarity against the knowledge base."""
    model = _fa_embedding_model()
    kb_embs = _fa_kb_embeddings()
    if model is None or kb_embs is None:
        return None
    try:
        import numpy as np
        query = f"{err} {step}".strip()[:512]
        q_emb = np.array(
            model.encode([query], normalize_embeddings=True, show_progress_bar=False),
            dtype="float32",
        )
        sims = (kb_embs @ q_emb.T).flatten()
        best_idx = int(np.argmax(sims))
        best_score = float(sims[best_idx])
        if best_score < 0.55:
            return None
        entry = _KNOWLEDGE_BASE[best_idx]
        confidence = min(0.95, entry.get("confidence", 0.70) * (best_score / 0.85))
        return FailureAnalysis(
            category=entry["category"],
            root_cause=entry["root_cause"],
            title=entry["title"],
            explanation=(
                f"Semantic analysis matched this failure to a known pattern "
                f"(similarity {best_score:.2f}).\n"
                f"Step: \"{_truncate(step, 120)}\"\n"
                f"Error: {_truncate(err)}"
            ),
            where=_where_for_category(entry["category"]),
            is_test_issue=entry.get("is_test_issue", False),
            suggested_fix=_fix_for_root_cause(entry["root_cause"]),
            confidence=confidence,
            analysis_method="semantic-ai",
        )
    except Exception as exc:
        _log.warning("Semantic match failed: %s", exc)
        return None


# ── Zero-shot pipeline ────────────────────────────────────────────────────
_FA_ZS_PIPELINE = None


def _fa_zero_shot():
    global _FA_ZS_PIPELINE
    if _FA_ZS_PIPELINE is not None:
        return _FA_ZS_PIPELINE
    try:
        from transformers import pipeline as hf_pipeline
        from app.config import settings
        _FA_ZS_PIPELINE = hf_pipeline(
            "zero-shot-classification",
            model=settings.ZEROSHOT_MODEL_EN,
            device=_cuda_device(),
        )
    except Exception as exc:
        _log.warning("Zero-shot pipeline not available: %s", exc)
    return _FA_ZS_PIPELINE


_ZS_LABELS = [
    "element not found or not visible",
    "timeout or synchronization issue",
    "assertion failed or value mismatch",
    "authentication or login failure",
    "network or server error",
    "malformed or unsupported test step",
]

_ZS_LABEL_MAP: dict[str, tuple] = {
    "element not found or not visible":   (CAT_DETECTION,   "ElementNotFound",      False),
    "timeout or synchronization issue":   (CAT_TIMING,      "WaitTimeoutExceeded",  False),
    "assertion failed or value mismatch": (CAT_APP,         "AssertionMismatch",    False),
    "authentication or login failure":    (CAT_APP,         "AuthenticationFailed", False),
    "network or server error":            (CAT_ENVIRONMENT, "NetworkError",         False),
    "malformed or unsupported test step": (CAT_TEST,        "InvalidScenarioStep",  True),
}


def _zero_shot_classify(err: str, step: str) -> "Optional[FailureAnalysis]":
    """Stage 3: zero-shot NLI classification over 6 failure categories."""
    clf = _fa_zero_shot()
    if clf is None:
        return None
    try:
        query = f"{step} — {err}"[:512]
        result = clf(query, candidate_labels=_ZS_LABELS, multi_label=False)
        top_label: str = result["labels"][0]
        top_score: float = result["scores"][0]
        if top_score < 0.70:
            return None
        cat, root, is_test = _ZS_LABEL_MAP.get(
            top_label, (CAT_UNKNOWN, "ClassifiedFailure", False)
        )
        return FailureAnalysis(
            category=cat,
            root_cause=root,
            title=f"Failure classified as: {top_label}",
            explanation=(
                f"Zero-shot ML classifier identified this failure as "
                f"'{top_label}' (score {top_score:.0%}).\n"
                f"Step: \"{_truncate(step, 120)}\"\n"
                f"Error: {_truncate(err)}"
            ),
            where=_where_for_category(cat),
            is_test_issue=is_test,
            suggested_fix=_fix_for_root_cause(root),
            confidence=min(0.92, top_score * 0.95),
            analysis_method="zero-shot-ai",
        )
    except Exception as exc:
        _log.warning("Zero-shot classification failed: %s", exc)
        return None


# ── Ollama LLM enrichment ─────────────────────────────────────────────────
def _ollama_analyze(
    err: str, step: str, selector: str, fallback_category: str
) -> "Optional[FailureAnalysis]":
    """Stage 4: Ollama/mistral structured failure analysis via JSON prompt."""
    if not _FA_OLLAMA_AVAILABLE:
        return None
    prompt = (
        "You are a senior QA engineer. Analyze this test failure and respond "
        "with a JSON object ONLY — no markdown, no extra text.\n\n"
        f"Failed step: {_truncate(step, 200)}\n"
        f"Error message: {_truncate(err, 400)}\n"
        f"Selector: {selector or 'none'}\n\n"
        "Respond ONLY with a valid JSON object with these exact keys:\n"
        '{\n'
        '  "category": "test|application|detection|timing|environment|unknown",\n'
        '  "root_cause": "ShortCamelCaseLabel",\n'
        '  "title": "One-line failure headline (max 80 chars)",\n'
        '  "explanation": "2-4 sentence explanation of why this test failed",\n'
        '  "is_test_issue": true or false,\n'
        '  "suggested_fix": "Concrete 2-3 step fix",\n'
        '  "confidence": 0.0 to 1.0\n'
        "}"
    )
    try:
        import json
        response = _ollama.chat(
            model="mistral",
            messages=[{"role": "user", "content": prompt}],
            options={"temperature": 0.3},
        )
        msg = response.message if hasattr(response, "message") else response["message"]
        text = (msg.content if hasattr(msg, "content") else msg["content"]).strip()
        start = text.find("{")
        end = text.rfind("}") + 1
        if start == -1 or end == 0:
            return None
        data = json.loads(text[start:end])
        cat = data.get("category", CAT_UNKNOWN)
        if cat not in (CAT_TEST, CAT_APP, CAT_DETECTION, CAT_TIMING, CAT_ENVIRONMENT, CAT_UNKNOWN):
            cat = CAT_UNKNOWN
        return FailureAnalysis(
            category=cat,
            root_cause=str(data.get("root_cause", "LLMClassified"))[:60],
            title=str(data.get("title", "LLM-analyzed failure"))[:120],
            explanation=str(data.get("explanation", ""))[:800],
            where=_where_for_category(cat),
            is_test_issue=bool(data.get("is_test_issue", False)),
            suggested_fix=str(data.get("suggested_fix", ""))[:400],
            confidence=min(0.95, max(0.4, float(data.get("confidence", 0.7)))),
            analysis_method="llm-ai",
        )
    except Exception:
        # Ollama not running or model not pulled — silently degrade
        return None


# ── Utility helpers shared by ML stages ──────────────────────────────────
def _where_for_category(cat: str) -> str:
    return {
        CAT_TEST:        "Scenario → NLP/Generator layer",
        CAT_APP:         "Application under test (functional behaviour)",
        CAT_DETECTION:   "Vision/Selector layer → DOM",
        CAT_TIMING:      "Playwright wait → DOM (synchronization)",
        CAT_ENVIRONMENT: "Browser → target application (network/DNS layer)",
        CAT_UNKNOWN:     "Unknown layer",
    }.get(cat, "Unknown layer")


def _fix_for_root_cause(root: str) -> str:
    _FIXES = {
        "ElementNotFound":        "Inspect the selector in your action mapping. Verify the element still exists in the DOM with the same text/role.",
        "WaitTimeoutExceeded":    "Add an explicit wait for the loading spinner to disappear. Consider increasing the action timeout.",
        "NavigationTimeout":      "Check the application URL is reachable. Increase the navigation timeout if the server is slow.",
        "AmbiguousSelector":      "Refine the step text to be more specific or add an Action Mapping with a precise selector (data-testid).",
        "AssertionMismatch":      "Re-run the scenario manually and compare the actual vs expected values. If the app is wrong, file a bug.",
        "AuthenticationFailed":   "Verify the credentials in the scenario are valid for the target environment.",
        "LoginRedirectFailed":    "The submit did not redirect — check the credentials and whether the submit button is enabled.",
        "DropdownValueMismatch":  "Run in headed mode to watch which option the When step selects. Ensure the Then step matches exactly.",
        "RowCountMismatch":       "Verify the test data state — check fixtures and pagination.",
        "NavigationFailure":      "Confirm the URL is reachable from the agent machine. Verify the server is running.",
        "NetworkError":           "Check network connectivity and server availability. Inspect HTTP response codes.",
        "InvalidScenarioStep":    "Rewrite the step using the supported Gherkin pattern. Split compound steps into separate lines.",
        "ClickIntercepted":       "Add a step to dismiss the blocking overlay (cookie banner, modal) before this action.",
        "ElementDetached":        "Wait for the async re-render to complete before locating the element.",
        "VisualFallbackFailed":   "Define an Action Mapping with a stable CSS selector or data-testid for this step.",
        "FormValidationError":    "Check that all required fields are filled before submitting the form.",
        "JavaScriptError":        "Open the browser console to inspect JS errors. This is likely an app bug.",
        "ElementDisabled":        "Verify the button/input should be enabled at this point in the flow. Check preconditions.",
        "ServerError":            "Check the server logs for the 5xx error. This is a back-end issue.",
        "DNSFailure":             "Verify the hostname is correct and DNS is reachable from the agent machine.",
        "ResourceNotFound":       "The requested URL returned 404. Verify the route exists and the app is deployed correctly.",
        "CORSError":              "Configure the server to allow requests from the test origin, or run tests on the same origin.",
        "ExpectedTextNotVisible": "Open the page manually and check if the text appears with the same casing and spacing.",
        "ModalNotShown":          "Verify the previous action correctly triggers the modal. Check for missing event handlers.",
        "LoginErrorMessageShown": "The app explicitly rejected the credentials. Verify username and password are correct.",
        "CheckboxStateMismatch":  "Verify the checkbox interaction step correctly toggles the element to the expected state.",
        "UnexpectedRedirect":     "Trace which action triggers the redirect. Verify the navigation logic in the application.",
        "ElementOutOfViewport":   "Scroll the element into view before interacting with it, or use page.scroll_into_view_if_needed().",
        "ElementNotInteractable": "Check whether the element is covered, hidden, or disabled at the point of interaction.",
        "IframeOrShadowDOM":      "Use a frame locator (page.frame_locator) to reach elements inside iframes.",
    }
    return _FIXES.get(
        root,
        "Inspect the screenshot taken at the failure point. Re-run with headless=false.",
    )


def analyze_step_failure(
    *,
    step_text: str,
    error_message: str,
    selector: str = "",
    keyword: str = "",
    visual_fallback_used: bool = False,
    retry_count: int = 0,
) -> FailureAnalysis:
    """Run the 4-stage ML cascade and return a structured analysis.

    Stage 1 — Regex rules        (deterministic, zero-latency)
    Stage 2 — SBERT semantic KB  (semantic-ai)
    Stage 3 — Zero-shot NLI      (zero-shot-ai)
    Stage 4 — Ollama/mistral LLM (llm-ai)
    Stage 5 — Fallback           (fallback)
    """
    err = (error_message or "").strip()
    step = (step_text or "").strip()

    # ── Stage 1: Regex rules ──────────────────────────────────────────────
    for pattern, builder in _RULES:
        if pattern.search(err):
            analysis = builder(err=err, step=step, selector=selector)
            analysis.analysis_method = "regex"
            if visual_fallback_used and analysis.category == CAT_DETECTION:
                analysis.confidence = min(0.95, analysis.confidence + 0.05)
            if retry_count >= 2 and analysis.category == CAT_TIMING:
                analysis.confidence = min(0.95, analysis.confidence + 0.05)
            return analysis

    # ── Stage 2: SBERT semantic similarity ───────────────────────────────
    result = _semantic_match(err, step, selector)
    if result is not None:
        if visual_fallback_used and result.category == CAT_DETECTION:
            result.confidence = min(0.95, result.confidence + 0.05)
        return result

    # ── Stage 3: Zero-shot ML classifier ─────────────────────────────────
    result = _zero_shot_classify(err, step)
    if result is not None:
        return result

    # ── Stage 4: Ollama LLM ───────────────────────────────────────────────
    result = _ollama_analyze(err, step, selector, CAT_UNKNOWN)
    if result is not None:
        return result

    # ── Stage 5: Fallback ─────────────────────────────────────────────────
    return FailureAnalysis(
        category=CAT_UNKNOWN,
        root_cause="UnclassifiedFailure",
        title="Step failed for an unclassified reason",
        explanation=(
            f"All analysis stages were unable to classify this failure.\n"
            f"Step: \"{_truncate(step, 160)}\"\n"
            f"Raw error: {_truncate(err)}"
        ),
        where="Unknown layer",
        is_test_issue=False,
        suggested_fix=(
            "• Inspect the screenshot taken at the failure point.\n"
            "• Re-run the scenario with `headless=false` to watch the failure live.\n"
            "• If the failure is reproducible, capture the raw error and add a "
            "rule for it in failure_analysis_service.py."
        ),
        confidence=0.3,
        analysis_method="fallback",
    )


def analyze_scenario_failure(
    *,
    scenario_name: str,
    failed_steps: list[dict],
) -> dict:
    """
    Aggregate one verdict for the whole scenario from its individual failed
    step analyses.  ``failed_steps`` items must each carry a key ``ai_analysis``
    set to the dict produced by ``analyze_step_failure``.
    """
    if not failed_steps:
        return {
            "category": CAT_UNKNOWN,
            "root_cause": "NoFailedSteps",
            "title": "Scenario passed",
            "explanation": "All steps completed successfully.",
            "is_test_issue": False,
            "suggested_fix": "",
            "confidence": 1.0,
            "failed_step_count": 0,
        }

    # Pick the analysis with the highest confidence as the primary verdict.
    primary = max(
        failed_steps,
        key=lambda s: (s.get("ai_analysis") or {}).get("confidence", 0.0),
    )
    primary_analysis = primary.get("ai_analysis") or {}

    return {
        **primary_analysis,
        "scenario_name": scenario_name,
        "failed_step_count": len(failed_steps),
        # Show the offending step text for context
        "first_failed_step": (primary.get("gherkin_text") or primary.get("step") or ""),
    }


__all__ = [
    "FailureAnalysis",
    "analyze_step_failure",
    "analyze_scenario_failure",
    "CAT_TEST",
    "CAT_APP",
    "CAT_DETECTION",
    "CAT_TIMING",
    "CAT_ENVIRONMENT",
    "CAT_UNKNOWN",
]
