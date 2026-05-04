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


def analyze_step_failure(
    *,
    step_text: str,
    error_message: str,
    selector: str = "",
    keyword: str = "",
    visual_fallback_used: bool = False,
    retry_count: int = 0,
) -> FailureAnalysis:
    """Run the rule engine and return a structured analysis.

    Always returns a FailureAnalysis — when no rule matches, falls back to a
    generic 'unknown' verdict that still asks the right questions.
    """
    err = (error_message or "").strip()
    step = (step_text or "").strip()

    # ── 1) Run through the ordered rules ──
    for pattern, builder in _RULES:
        if pattern.search(err):
            analysis = builder(err=err, step=step, selector=selector)
            # Boost confidence slightly if the visual fallback was already used —
            # means the heuristic engine already tried hard before failing.
            if visual_fallback_used and analysis.category == CAT_DETECTION:
                analysis.confidence = min(0.95, analysis.confidence + 0.05)
            # If we retried multiple times, timing / app issues are more likely.
            if retry_count >= 2 and analysis.category == CAT_TIMING:
                analysis.confidence = min(0.95, analysis.confidence + 0.05)
            return analysis

    # ── 2) Fallback: generic but useful ──
    return FailureAnalysis(
        category=CAT_UNKNOWN,
        root_cause="UnclassifiedFailure",
        title="Step failed for an unclassified reason",
        explanation=(
            f"The agent could not match this error against a known failure "
            f"pattern.\n"
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
        confidence=0.4,
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
