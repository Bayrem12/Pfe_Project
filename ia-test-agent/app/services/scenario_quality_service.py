"""
Scenario quality analysis service — 4-stage cascade.

Given a Gherkin scenario (name + steps), produce a structured quality report:

  • quality_score    — 0-100
  • quality_label    — "good" | "medium" | "poor"
  • issues           — list of IssueData (error / warning / info)
  • suggestions      — list of actionable strings
  • improved_steps   — rewritten scenario steps
  • best_practices   — general Gherkin tips
  • analysis_method  — regex | semantic-ai | zero-shot-ai | llm-ai

Analysis cascade:
  Stage 1 – Regex rule engine   (always runs, 20+ checks)
  Stage 2 – SBERT semantic KB   (lazy singleton; cosine ≥ 0.60)
  Stage 3 – Zero-shot NLI       (facebook/bart-large-mnli; threshold 0.65)
  Stage 4 – Ollama / mistral    (JSON prompt; degrades gracefully)
"""

from __future__ import annotations

import json
import re
import threading
from dataclasses import dataclass, field
from typing import Any

# ── Optional ML support ────────────────────────────────────────────────────
try:
    from sentence_transformers import SentenceTransformer, util as st_util  # type: ignore
    _SBERT_AVAILABLE = True
except ImportError:
    _SBERT_AVAILABLE = False

try:
    from transformers import pipeline as hf_pipeline  # type: ignore
    _ZS_AVAILABLE = True
except ImportError:
    _ZS_AVAILABLE = False

try:
    import ollama as _ollama  # type: ignore
    _OLLAMA_AVAILABLE = True
except ImportError:
    _OLLAMA_AVAILABLE = False


# ── Device selection (GPU if available, CPU otherwise) ─────────────────────
def _cuda_device() -> int:
    """Return 0 (first GPU) when CUDA is available, -1 (CPU fallback)."""
    try:
        import torch
        if torch.cuda.is_available():
            import logging as _logging
            _logging.getLogger(__name__).info(
                "CUDA available – ML models will use GPU (%s)",
                torch.cuda.get_device_name(0),
            )
            return 0
    except Exception:
        pass
    return -1


# ── Domain dataclasses ─────────────────────────────────────────────────────

@dataclass
class StepInput:
    keyword: str
    text: str


@dataclass
class IssueData:
    severity: str           # "error" | "warning" | "info"
    step_index: int | None  # None = scenario-level issue
    step_text: str | None
    message: str
    why: str


@dataclass
class ImprovedStepData:
    keyword: str
    text: str


@dataclass
class QualityResult:
    quality_score: int
    quality_label: str                          # "good" | "medium" | "poor"
    issues: list[IssueData] = field(default_factory=list)
    suggestions: list[str] = field(default_factory=list)
    improved_steps: list[ImprovedStepData] = field(default_factory=list)
    best_practices: list[str] = field(default_factory=list)
    using_llm: bool = False
    analysis_method: str = "regex"              # regex | semantic-ai | zero-shot-ai | llm-ai


# ── SBERT lazy singleton + anti-pattern KB ────────────────────────────────

_sbert_lock = threading.Lock()
_sbert_model: Any = None
_sbert_kb_embeddings: Any = None

# (pattern_text, severity, issue_message, why)
_SBERT_ANTI_PATTERNS: list[tuple[str, str, str, str]] = [
    ("click button",
     "warning",
     "Vague click target — which button?",
     "Without a precise target name the executor cannot identify the element."),
    ("press the button",
     "warning",
     "Generic button reference — specify its label.",
     "Generic element references cause selector ambiguity and brittle tests."),
    ("enter data into field",
     "info",
     "Unquoted field value in step.",
     'Use quoted values: I enter "admin@example.com" in the email field.'),
    ("fill in the form",
     "warning",
     '"Fill in the form" is too coarse — name each field.',
     "Specify each field individually for precise test generation."),
    ("verify the page",
     "error",
     '"Verify the page" is not a measurable assertion.',
     'Use a specific element or text: I should see "Dashboard".'),
    ("check the result",
     "warning",
     '"Check the result" lacks a concrete expected value.',
     "Specify the expected outcome explicitly."),
    ("the system should work",
     "error",
     '"Should work" is not testable.',
     "Replace with a specific observable outcome."),
    ("everything works fine",
     "error",
     "Vague pass criterion — executor cannot verify 'everything works'.",
     "Each Then step must assert one specific, observable outcome."),
    ("navigate to page",
     "info",
     "Navigation step lacks a specific URL or page name.",
     'Prefer: "I navigate to the dashboard page" or "I go to /dashboard".'),
    ("click on link",
     "info",
     '"Click on link" needs the visible link text.',
     'Use the visible text of the link: "I click on the Forgot Password link".'),
    ("the user is logged in",
     "info",
     "Implicit login state — add an explicit login step.",
     "Make the test self-contained by explicitly authenticating."),
    ("user clicks submit",
     "warning",
     "Use first-person 'I' instead of 'user'.",
     "BDD steps use first-person I to align with natural-language conventions."),
    ("user enters username",
     "info",
     "Use first-person 'I enter' instead of 'user enters'.",
     "First-person active voice keeps Gherkin readable and consistent."),
    ("see a message",
     "info",
     '"See a message" — quote the exact message text.',
     'Use: I should see "Login successful" to create an unambiguous assertion.'),
    ("is displayed successfully",
     "info",
     '"Successfully" is a vague qualifier in assertions.',
     "Remove 'successfully' and assert the specific visible element or text."),
    ("do something",
     "error",
     '"Do something" is entirely untestable.',
     "Replace with a concrete action step."),
    ("perform the action",
     "warning",
     '"Perform the action" is too abstract.',
     "Name the specific user action to perform."),
    ("open the application",
     "info",
     '"Open the application" lacks a specific URL.',
     'Use: "I navigate to https://example.com" or "I navigate to the login page".'),
    ("it should appear",
     "warning",
     '"It should appear" — what is \'it\'?',
     "Always specify the element or text that should appear."),
    ("verify login works",
     "error",
     '"Verify login works" is a non-observable assertion.',
     "Assert a specific outcome: 'I should see the dashboard page'."),
    ("click somewhere",
     "error",
     "Completely undefined click target.",
     "Provide the name or label of the element to click."),
    ("scroll down",
     "info",
     '"Scroll down" without a target is non-deterministic.',
     'Add a target: "I scroll down to the footer section".'),
    ("wait for page",
     "warning",
     '"Wait for page" is a timing anti-pattern.',
     "Use an assertion-based wait: 'I should see the dashboard'."),
    ("the test passes",
     "error",
     '"The test passes" tells the executor nothing.',
     "Assert a specific observable outcome of the user action."),
    ("and then the user",
     "warning",
     "Compound step — should be split into two steps.",
     "Each step should contain exactly one action for maintainability."),
    ("something happens",
     "error",
     '"Something happens" is completely untestable.',
     "Describe the exact observable change that should occur."),
    ("the form is submitted",
     "info",
     "Passive voice — prefer 'I submit the form'.",
     "Active voice with 'I' is clearer and more consistent with BDD conventions."),
    ("I click the thing",
     "warning",
     '"The thing" is a generic reference.',
     "Name the element by its visible label or role."),
    ("it works",
     "error",
     '"It works" is not a testable assertion.',
     "Specify precisely what outcome proves correctness."),
]

_SBERT_THRESHOLD = 0.60


def _ensure_sbert() -> bool:
    global _sbert_model, _sbert_kb_embeddings
    if _sbert_model is not None:
        return True
    with _sbert_lock:
        if _sbert_model is not None:
            return True
        if not _SBERT_AVAILABLE:
            return False
        try:
            _sbert_model = SentenceTransformer(
                "paraphrase-multilingual-MiniLM-L12-v2",
                device=_cuda_device(),
            )
            texts = [p[0] for p in _SBERT_ANTI_PATTERNS]
            _sbert_kb_embeddings = _sbert_model.encode(
                texts, convert_to_tensor=True, normalize_embeddings=True
            )
            return True
        except Exception:
            return False


def _sbert_check_batch(candidates: list[tuple[int, str]]) -> list[IssueData]:
    """Batch-encode candidate steps and find anti-pattern matches."""
    if not _ensure_sbert() or not candidates:
        return []
    try:
        texts = [t for _, t in candidates]
        embs = _sbert_model.encode(texts, convert_to_tensor=True, normalize_embeddings=True)
        scores = st_util.cos_sim(embs, _sbert_kb_embeddings)  # (n_steps, n_kb)
        found: list[IssueData] = []
        for i, (step_idx, step_text) in enumerate(candidates):
            best_kb = int(scores[i].argmax())
            best_score = float(scores[i][best_kb])
            if best_score >= _SBERT_THRESHOLD:
                _, sev, msg, why = _SBERT_ANTI_PATTERNS[best_kb]
                found.append(IssueData(
                    severity=sev, step_index=step_idx, step_text=step_text,
                    message=msg, why=why,
                ))
        return found
    except Exception:
        return []


# ── Zero-shot NLI lazy singleton ──────────────────────────────────────────

_zs_lock = threading.Lock()
_zs_classifier: Any = None

_ZS_LABELS = [
    "is a clear and testable step",
    "is a vague or ambiguous step",
    "contains multiple unrelated actions",
    "references a generic non-specific UI element",
]
_ZS_THRESHOLD = 0.65


def _ensure_zs() -> bool:
    global _zs_classifier
    if _zs_classifier is not None:
        return True
    with _zs_lock:
        if _zs_classifier is not None:
            return True
        if not _ZS_AVAILABLE:
            return False
        try:
            _zs_classifier = hf_pipeline(
                "zero-shot-classification",
                model="facebook/bart-large-mnli",
                device=_cuda_device(),
            )
            return True
        except Exception:
            return False


def _zs_check_batch(candidates: list[tuple[int, str]]) -> list[IssueData]:
    """Batch zero-shot classification for a list of (step_index, step_text) pairs."""
    if not _ensure_zs() or not candidates:
        return []
    try:
        texts = [t for _, t in candidates]
        results = _zs_classifier(
            texts,
            candidate_labels=_ZS_LABELS,
            hypothesis_template="This step {}",
            multi_label=False,
        )
        if isinstance(results, dict):  # single result wrapped in dict
            results = [results]

        found: list[IssueData] = []
        for (step_idx, step_text), res in zip(candidates, results):
            best_label: str = res["labels"][0]
            best_score: float = res["scores"][0]
            if best_score < _ZS_THRESHOLD or best_label == "is a clear and testable step":
                continue
            if best_label == "contains multiple unrelated actions":
                sev, msg, why = (
                    "warning",
                    f"Compound step (AI-detected): '{step_text[:70]}'",
                    "Split compound steps so each step contains exactly one action.",
                )
            elif best_label == "is a vague or ambiguous step":
                sev, msg, why = (
                    "warning",
                    f"Ambiguous step (AI-detected): '{step_text[:70]}'",
                    "Zero-shot analysis flagged this step as unclear. Add concrete values and targets.",
                )
            else:
                sev, msg, why = (
                    "info",
                    f"Generic UI reference (AI-detected): '{step_text[:70]}'",
                    "Reference specific named elements rather than generic ones like 'the button'.",
                )
            found.append(IssueData(
                severity=sev, step_index=step_idx, step_text=step_text,
                message=msg, why=why,
            ))
        return found
    except Exception:
        return []


# ── Rule engine patterns ──────────────────────────────────────────────────

_VAGUE_ACTION_PATTERNS = [
    (re.compile(r'\bclick\b(?!\s+on\b)', re.I),
     'Use "click on the [element name]" to be explicit about the target.'),
    (re.compile(r'\bsubmit\b(?!\s+the\b|\s+form\b)', re.I),
     'Use "click on the submit button" or "submit the [form name] form".'),
    (re.compile(r'\bworks?\b', re.I),
     '"works" is not testable. Use a concrete assertion like "I should see … message".'),
    (re.compile(r'\bsuccessfully?\b', re.I),
     '"successfully" is vague. Replace with an observable outcome assertion.'),
    (re.compile(r'\bsomething\b', re.I),
     '"something" is ambiguous. Specify the exact element or value.'),
    (re.compile(r'\bthe (page|site|system)\s+(works?|loads?|runs?|opens?)\b', re.I),
     'Vague outcome. Assert a specific element, URL change or message.'),
    (re.compile(r'\bdo (something|anything|whatever)\b', re.I),
     '"do something" is entirely untestable. Name the specific action.'),
    (re.compile(r'\bcorrectly\b', re.I),
     '"correctly" is vague. Assert the specific expected behavior.'),
]

_VAGUE_FILL_PATTERNS = re.compile(
    r'\b(enter|type|fill|input)\b.{0,40}\b(username|email|password|credentials?)\b',
    re.I,
)
_QUOTED_VALUE = re.compile(r'["\']([^"\']+)["\']|"([^"]+)"')

_ASSERT_KEYWORDS = re.compile(
    r'\b(see|visible|display|contain|show|assert|verify|check|confirm|redirect|'
    r'navigate to|should be|should have|should see|should not|'
    r'affiche|devrait|voit?|vérifie|contient|est redirigé)\b',
    re.I,
)
_LOGIN_STEP = re.compile(
    r'\b(log in|login|sign in|signin|connexion|se connecter|authenticate)\b', re.I
)
_NAV_STEP = re.compile(
    r'\b(navigate|go to|open|visit|accède|navigu|ouvre)\b', re.I
)
_COMPOUND_STEP = re.compile(
    r'\band\s+then\b|\band\s+also\b|,\s*then\b', re.I
)
_GENERIC_UI_REF = re.compile(
    r'\bthe\s+(button|link|field|element|input|checkbox|dropdown|select|icon|tab)\b(?!\s+\w)',
    re.I,
)
_PASSIVE_ASSERT = re.compile(
    r'\b(is|are|was|were)\s+(displayed|shown|visible|rendered|presented|loaded)\b',
    re.I,
)
_THIRD_PERSON = re.compile(
    r'^(?:the\s+)?(?:user|system|app|application)\s+', re.I
)
_VAGUE_NAME_RE = re.compile(
    r'^(test|scenario|my test|test scenario|scenario\s*\d+|new scenario|untitled|example)$',
    re.I,
)
_ACTION_VERB_START = re.compile(
    r'^(click|enter|fill|type|select|choose|navigate|go|open|visit|submit|check|'
    r'uncheck|toggle|scroll|drag|drop|upload|download|search|filter|'
    r'cliquer|saisir|remplir|sélectionner|naviguer|aller|ouvrir|soumettre)\b',
    re.I,
)

# Words/phrases that render the entire step untestable
_PURELY_VAGUE_RE = re.compile(
    r'^\s*(it|the\s+page|the\s+site|the\s+system|everything)?\s*'
    r'(works?|runs?|loads?|is\s+ok|is\s+fine|does\s+something|'
    r'successfully|successful|something)\s*'
    r'(successfully|fine|ok|well)?\s*\.?\s*$',
    re.I,
)


# ── Internal helpers ──────────────────────────────────────────────────────

def _classify_keyword(kw: str) -> str:
    kw = kw.strip().upper()
    if kw in ("GIVEN", "AND", "BUT"):
        return "given"
    if kw == "WHEN":
        return "when"
    if kw == "THEN":
        return "then"
    return kw.lower()


def _is_purely_vague(text: str) -> bool:
    """True when the step text contains nothing concrete to test."""
    return bool(_PURELY_VAGUE_RE.match(text.strip()))


def _split_compound(text: str) -> list[str]:
    """Split a compound step on 'and then' / 'and also' / ', then'."""
    for sep in (r'\band\s+then\b', r'\band\s+also\b', r',\s*then\b'):
        parts = re.split(sep, text, flags=re.I)
        if len(parts) > 1:
            return [p.strip() for p in parts if p.strip()]
    return [text]


def _to_first_person(text: str) -> str:
    """Best-effort: 'the user clicks X' → 'I click X'."""
    m = re.match(r'^(?:the\s+)?(?:user|system|app|application)\s+', text, re.I)
    if not m:
        return text
    remainder = text[m.end():]
    _IRREGULARS = {'is', 'does', 'goes', 'has', 'was', 'were', 'says', 'knows', 'shows'}
    verb_m = re.match(r'^(\w+)', remainder)
    if verb_m:
        verb = verb_m.group(1)
        if verb.lower() not in _IRREGULARS and verb.endswith('s') and not verb.endswith('ss'):
            remainder = verb[:-1] + remainder[len(verb):]
    return "I " + remainder


def _fix_step_text(text: str, kw_class: str) -> str:
    """Apply rule-based fixes to a single step's text."""
    text = _to_first_person(text)

    # Canonicalize single-quoted values → double quotes
    text = re.sub(r"'([^']+)'", r'"\1"', text)

    # Fix: bare "click X" → "click on the X"
    text = re.sub(r'\bclick\b(?!\s+on\b)\s+(.+)', r'click on the \1', text, flags=re.I)

    # Fix: bare "submit" → "click on the submit button"
    text = re.sub(r'^submit$', 'click on the submit button', text, flags=re.I)

    if kw_class == "then":
        # Passive: "the X is displayed" → "I should see the X"
        text = re.sub(
            r'^(the\s+.+?)\s+(is|are)\s+(displayed|shown|visible|rendered)\s*$',
            r'I should see \1',
            text,
            flags=re.I,
        )
        text = re.sub(r'\bsuccessfully\b', '', text, flags=re.I).strip()
        text = re.sub(r'\bworks?\b', '', text, flags=re.I).strip()
        text = re.sub(r'\s{2,}', ' ', text).strip()
        if not text or _is_purely_vague(text):
            return 'I should see a confirmation or success message'
        if not _ASSERT_KEYWORDS.search(text) and not _PASSIVE_ASSERT.search(text):
            text = f'I should see {text}'
    else:
        if not text.lower().startswith('i ') and _ACTION_VERB_START.match(text):
            text = 'I ' + text

    return text.strip()


def _best_practices() -> list[str]:
    return [
        'Use precise action verbs: "click on", "fill in", "select", "submit".',
        'Always add an explicit Then assertion — never rely on the absence of errors.',
        'Quote exact values in steps: I enter "admin@example.com" in the email field.',
        'Start with a Given that sets navigation context (e.g. "I navigate to /login").',
        'Avoid vague words: "works", "successfully", "something", "the page loads".',
        'One observable outcome per Then step keeps failures easy to diagnose.',
        'Use "And" to chain related steps rather than packing multiple actions in one.',
        'Name elements by their visible label: "the Login button", not "the button".',
        'Use first-person "I" for all actor steps (Given / When / Then).',
        'Each When step should contain exactly one user action.',
        'Assert specific text: I should see "Welcome, admin!" not "I should see a message".',
        'Keep scenarios under 12 steps — longer scenarios should be split.',
        'Avoid hardcoded wait steps; use assertion-based waits instead.',
        'Use a descriptive scenario name that captures the user goal and expected outcome.',
        'Review improved-scenario suggestions and apply fixes before running tests.',
    ]


# ── Step improvement ──────────────────────────────────────────────────────

def _improve_steps(
    steps: list[StepInput],
    has_given: bool,
    has_when: bool,
    has_then: bool,
    has_nav: bool,
) -> list[ImprovedStepData]:
    improved: list[ImprovedStepData] = []

    if not has_given and not has_nav:
        improved.append(ImprovedStepData(keyword="Given", text="I navigate to the application URL"))

    for step in steps:
        text = step.text.strip()
        if not text:
            continue
        kw = step.keyword
        kw_class = _classify_keyword(kw)

        if _is_purely_vague(text) and kw_class != "then":
            continue

        parts = _split_compound(text)
        if len(parts) > 1:
            for i, part in enumerate(parts):
                part_kw = kw if i == 0 else "And"
                fixed = _fix_step_text(part, _classify_keyword(part_kw))
                improved.append(ImprovedStepData(keyword=part_kw, text=fixed))
            continue

        fixed = _fix_step_text(text, kw_class)
        improved.append(ImprovedStepData(keyword=kw, text=fixed))

    if not has_then:
        improved.append(ImprovedStepData(
            keyword="Then",
            text="I should see a confirmation or success message",
        ))

    return improved


# ── Stage 4: Ollama LLM enhancement ──────────────────────────────────────

def _enhance_with_llm(
    scenario_name: str,
    steps: list[StepInput],
    issues: list[IssueData],
) -> tuple[list[str], list[ImprovedStepData], bool]:
    """Returns (extra_suggestions, improved_steps, used_llm). Degrades gracefully."""
    if not _OLLAMA_AVAILABLE:
        return [], [], False

    try:
        step_lines = "\n".join(
            f"  {s.keyword} {s.text}" for s in steps if s.text.strip()
        ) or "  (no steps provided)"

        issue_lines = (
            "\n".join(f"  [{i.severity.upper()}] {i.message}" for i in issues[:8])
            if issues
            else "  None detected"
        )

        prompt = (
            f'You are a Gherkin BDD quality expert reviewing a scenario named "{scenario_name}".\n\n'
            f"Scenario steps:\n{step_lines}\n\n"
            f"Pre-detected issues:\n{issue_lines}\n\n"
            "Your task:\n"
            "1. Provide 2-3 concise actionable improvement suggestions (new ones, not repeating issues).\n"
            "2. Rewrite the full scenario applying all improvements.\n\n"
            "Reply ONLY with valid JSON — no extra text, no markdown fences:\n"
            "{\n"
            '  "suggestions": ["suggestion 1", "suggestion 2"],\n'
            '  "improved_steps": [\n'
            '    {"keyword": "Given", "text": "step text"},\n'
            '    {"keyword": "When", "text": "step text"},\n'
            '    {"keyword": "Then", "text": "step text"}\n'
            "  ]\n"
            "}\n\n"
            "Rules for improved_steps:\n"
            "- keyword must be one of: Given, When, Then, And, But\n"
            "- Use first-person 'I'\n"
            "- Quote exact values (double quotes)\n"
            "- One action per step\n"
            "- Always include at least one Then assertion"
        )

        response = _ollama.chat(
            model="mistral",
            messages=[{"role": "user", "content": prompt}],
            options={"temperature": 0.2},
        )

        msg = response.message if hasattr(response, "message") else response["message"]
        raw: str = (msg.content if hasattr(msg, "content") else msg["content"]).strip()

        raw = re.sub(r'^```(?:json)?\s*', '', raw, flags=re.I)
        raw = re.sub(r'\s*```$', '', raw, flags=re.I).strip()

        json_match = re.search(r'\{.*\}', raw, re.DOTALL)
        if not json_match:
            return [], [], False

        data = json.loads(json_match.group(0))

        suggestions: list[str] = [
            s for s in data.get("suggestions", [])
            if isinstance(s, str) and len(s) > 10
        ][:3]

        _VALID_KWS = {"given", "when", "then", "and", "but"}
        improved_steps: list[ImprovedStepData] = []
        for obj in data.get("improved_steps", []):
            if not isinstance(obj, dict):
                continue
            kw = str(obj.get("keyword", "")).strip().capitalize()
            txt = str(obj.get("text", "")).strip()
            if kw.lower() in _VALID_KWS and txt:
                improved_steps.append(ImprovedStepData(keyword=kw, text=txt))

        used = bool(suggestions or improved_steps)
        return suggestions, improved_steps, used

    except Exception:
        return [], [], False


# ── Public API ────────────────────────────────────────────────────────────

def analyze_quality(  # noqa: C901
    scenario_name: str,
    steps: list[StepInput],
    language: str = "en",
) -> QualityResult:
    issues: list[IssueData] = []
    suggestions: list[str] = []
    analysis_method = "regex"

    # ── 1. Empty / no steps ───────────────────────────────────────────────
    if not steps:
        return QualityResult(
            quality_score=0,
            quality_label="poor",
            issues=[IssueData(
                severity="error", step_index=None, step_text=None,
                message="Scenario has no steps.",
                why="An empty scenario cannot be executed and will always pass vacuously.",
            )],
            suggestions=["Add at least Given / When / Then steps."],
            improved_steps=[],
            best_practices=_best_practices(),
            analysis_method="regex",
        )

    non_empty = [s for s in steps if s.text.strip()]
    if not non_empty:
        issues.append(IssueData(
            severity="error", step_index=None, step_text=None,
            message="All steps are empty.",
            why="Steps without text generate no executable code.",
        ))

    # ── 2. Scenario name check ────────────────────────────────────────────
    name = scenario_name.strip()
    if not name:
        issues.append(IssueData(
            severity="info", step_index=None, step_text=None,
            message="Scenario has no name.",
            why="Named scenarios make failure reports easier to understand.",
        ))
    elif len(name) < 10 or _VAGUE_NAME_RE.match(name):
        issues.append(IssueData(
            severity="info", step_index=None, step_text=None,
            message=f'Scenario name is too generic: "{name}".',
            why="Use a descriptive name that captures the user goal and expected outcome.",
        ))

    # ── 3. Given / When / Then structure ─────────────────────────────────
    classified = [(_classify_keyword(s.keyword), s) for s in steps if s.text.strip()]
    has_given = any(k == "given" for k, _ in classified)
    has_when  = any(k == "when"  for k, _ in classified)
    has_then  = any(k == "then"  for k, _ in classified)

    if not has_given:
        issues.append(IssueData(
            severity="warning", step_index=None, step_text=None,
            message="Missing GIVEN (precondition) step.",
            why="Without a Given step the initial state is undefined, making the test unreliable.",
        ))
        suggestions.append('Add a Given step to set up the initial context, e.g. "Given I navigate to the login page".')

    if not has_when:
        issues.append(IssueData(
            severity="error", step_index=None, step_text=None,
            message="Missing WHEN (action) step.",
            why="No user action means the scenario tests nothing.",
        ))
        suggestions.append("Add at least one WHEN step describing the user's action.")

    if not has_then:
        issues.append(IssueData(
            severity="error", step_index=None, step_text=None,
            message="Missing THEN (assertion) step.",
            why="Without an assertion the test always passes regardless of outcome — a classic false positive.",
        ))
        suggestions.append('Add a Then step that verifies the expected outcome, e.g. "Then I should see a success message".')

    # ── 4. Scenario length ─────────────────────────────────────────────────
    if len(non_empty) > 12:
        issues.append(IssueData(
            severity="warning", step_index=None, step_text=None,
            message=f"Scenario has {len(non_empty)} steps — consider splitting it.",
            why="Long scenarios (>12 steps) are harder to maintain and harder to debug when they fail.",
        ))
        suggestions.append("Split this scenario into smaller scenarios, each covering one user behaviour.")

    # ── 5. Too many WHEN steps ─────────────────────────────────────────────
    when_count = sum(1 for k, _ in classified if k == "when")
    if when_count > 4:
        issues.append(IssueData(
            severity="warning", step_index=None, step_text=None,
            message=f"Too many WHEN steps ({when_count}) — a scenario should focus on one user action.",
            why="Multiple When steps indicate a test that tries to cover too much in one scenario.",
        ))

    # ── 6. Navigation / login precondition ───────────────────────────────
    step_texts = " ".join(s.text for s in steps if s.text.strip()).lower()
    has_nav          = bool(_NAV_STEP.search(step_texts))
    has_login_action = bool(_LOGIN_STEP.search(step_texts))

    if has_login_action and not has_nav:
        suggestions.append(
            'Add "Given I navigate to the login page" before login actions so the test knows where to start.'
        )

    # ── 7. Per-step analysis ──────────────────────────────────────────────
    seen_texts: dict[str, int] = {}
    sbert_candidates: list[tuple[int, str]] = []

    for idx, step in enumerate(steps):
        text = step.text.strip()
        if not text:
            issues.append(IssueData(
                severity="warning", step_index=idx, step_text=None,
                message=f"Step {idx + 1} is empty.",
                why="Empty steps are skipped by the generator and create gaps in execution.",
            ))
            continue

        kw_class = _classify_keyword(step.keyword)
        issues_before = len(issues)

        # 7a. Duplicate steps
        norm = re.sub(r'\s+', ' ', text).lower()
        if norm in seen_texts:
            issues.append(IssueData(
                severity="warning", step_index=idx, step_text=text,
                message=f'Step {idx + 1} is a duplicate of step {seen_texts[norm] + 1}.',
                why="Duplicate steps add no coverage and bloat the scenario.",
            ))
        else:
            seen_texts[norm] = idx

        # 7b. Overly long step (>18 words)
        word_count = len(text.split())
        if word_count > 18:
            issues.append(IssueData(
                severity="info", step_index=idx, step_text=text,
                message=f"Step {idx + 1} is too verbose ({word_count} words).",
                why="Long steps are hard to read and often try to do too much at once.",
            ))
            suggestions.append(f"Shorten step {idx + 1}: split it or remove redundant words.")

        # 7c. Compound step ("and then" / ", then")
        if _COMPOUND_STEP.search(text):
            issues.append(IssueData(
                severity="warning", step_index=idx, step_text=text,
                message=f'Step {idx + 1} contains multiple actions ("and then").',
                why="Compound steps are harder to reuse and harder to debug when they fail.",
            ))
            suggestions.append(f"Split step {idx + 1} into two separate steps.")

        # 7d. Generic UI reference
        generic_m = _GENERIC_UI_REF.search(text)
        if generic_m:
            issues.append(IssueData(
                severity="info", step_index=idx, step_text=text,
                message=f'Step {idx + 1}: generic element reference "{generic_m.group(0).strip()}".',
                why='Name the element by its visible label, e.g. "the Login button".',
            ))

        # 7e. Third-person subject
        if _THIRD_PERSON.match(text) and kw_class in ("when", "then"):
            issues.append(IssueData(
                severity="info", step_index=idx, step_text=text,
                message=f'Step {idx + 1}: use first-person "I" instead of "user/system".',
                why="BDD convention uses first-person 'I' for all actor steps.",
            ))

        # 7f. Vague action words
        for pattern, advice in _VAGUE_ACTION_PATTERNS:
            if pattern.search(text):
                issues.append(IssueData(
                    severity="warning", step_index=idx, step_text=text,
                    message=f'Vague or implicit action in step {idx + 1}: "{text[:70]}".',
                    why=advice,
                ))

        # 7g. Fill/enter without quoted value
        if _VAGUE_FILL_PATTERNS.search(text) and not _QUOTED_VALUE.search(text):
            issues.append(IssueData(
                severity="info", step_index=idx, step_text=text,
                message=f'Step {idx + 1}: credential value not specified (use quotes).',
                why='Without a quoted value the AI cannot extract the text to type. '
                    'Use: I enter "admin" in the username field.',
            ))

        # 7h. THEN without observable assertion
        if kw_class == "then":
            if not _ASSERT_KEYWORDS.search(text):
                if _PASSIVE_ASSERT.search(text):
                    issues.append(IssueData(
                        severity="info", step_index=idx, step_text=text,
                        message=f'Step {idx + 1}: passive assertion. Prefer active voice.',
                        why='Instead of "the message is displayed", write "I should see the message".',
                    ))
                else:
                    issues.append(IssueData(
                        severity="error", step_index=idx, step_text=text,
                        message=f'Then step lacks an assertion keyword: "{text[:70]}".',
                        why="Without a verification keyword the step produces no assertion — the test will always pass.",
                    ))
                    suggestions.append(
                        f'Rewrite step {idx + 1} to start with "I should see …" or "I should be redirected to …".'
                    )

        # 7i. "etc." or "..." signals incomplete step
        if re.search(r'\betc\.?\b|\.\.\.', text, re.I):
            issues.append(IssueData(
                severity="warning", step_index=idx, step_text=text,
                message=f'Step {idx + 1}: "etc." or "..." indicates an incomplete definition.',
                why="Incomplete steps generate incomplete test code. Be explicit about every action and value.",
            ))

        if len(issues) == issues_before:
            sbert_candidates.append((idx, text))

    # ── Stage 2: SBERT semantic anti-pattern check ─────────────────────────
    sbert_issues = _sbert_check_batch(sbert_candidates)
    if sbert_issues:
        analysis_method = "semantic-ai"

    # ── Stage 3: Zero-shot on still-clean steps ───────────────────────────
    sbert_flagged = {i.step_index for i in sbert_issues}
    zs_candidates = [(idx, t) for idx, t in sbert_candidates if idx not in sbert_flagged]
    zs_issues = _zs_check_batch(zs_candidates)
    if zs_issues:
        analysis_method = "zero-shot-ai"

    issues = issues + sbert_issues + zs_issues

    # ── 8. Score calculation ──────────────────────────────────────────────
    error_count   = sum(1 for i in issues if i.severity == "error")
    warning_count = sum(1 for i in issues if i.severity == "warning")
    info_count    = sum(1 for i in issues if i.severity == "info")

    score = 100 - (error_count * 20) - (warning_count * 8) - (info_count * 2)
    score = max(0, min(100, score))

    label = "good" if score >= 75 else "medium" if score >= 45 else "poor"

    # ── 9. Improved steps ────────────────────────────────────────────────
    improved = _improve_steps(steps, has_given, has_when, has_then, has_nav)

    # ── 10. Global suggestions (deduplicated) ─────────────────────────────
    if not suggestions:
        if score == 100:
            suggestions.append("Great scenario! All steps are clear and testable.")
        else:
            suggestions.append("Review the issues above and add explicit assertions to your Then steps.")

    # ── 11. Stage 4: Ollama LLM enhancement ──────────────────────────────
    llm_suggestions, llm_improved, using_llm = _enhance_with_llm(scenario_name, steps, issues)
    all_suggestions = list(dict.fromkeys(suggestions + llm_suggestions))
    if using_llm:
        analysis_method = "llm-ai"

    final_improved = llm_improved if llm_improved else improved

    return QualityResult(
        quality_score=score,
        quality_label=label,
        issues=issues,
        suggestions=all_suggestions,
        improved_steps=final_improved,
        best_practices=_best_practices(),
        using_llm=using_llm,
        analysis_method=analysis_method,
    )
