"""
POST /api/ia/analyze-quality
Analyzes a Gherkin scenario for quality, completeness and testability
before any test execution. Returns a quality score, detected issues,
improvement suggestions and a rewritten improved scenario.
"""

from __future__ import annotations

import re
from typing import Any

from fastapi import APIRouter
from pydantic import BaseModel

router = APIRouter(prefix="/api/ia", tags=["Scenario Quality"])


# ── Request / Response schemas ─────────────────────────────────────────────

class QualityStep(BaseModel):
    keyword: str
    text: str


class QualityRequest(BaseModel):
    scenario_name: str = ""
    steps: list[QualityStep] = []
    language: str = "en"


class QualityIssue(BaseModel):
    severity: str          # "error" | "warning" | "info"
    step_index: int | None  # None = scenario-level issue
    step_text: str | None
    message: str
    why: str               # Why it can cause false positives / failures


class ImprovedStep(BaseModel):
    keyword: str
    text: str


class QualityResponse(BaseModel):
    quality_score: int                   # 0-100
    quality_label: str                   # "good" | "medium" | "poor"
    issues: list[QualityIssue]
    suggestions: list[str]
    improved_steps: list[ImprovedStep]
    best_practices: list[str]


# ── Rule engine ────────────────────────────────────────────────────────────

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


def _classify_keyword(kw: str) -> str:
    kw = kw.strip().upper()
    if kw in ("GIVEN", "AND", "BUT"):
        return "given"
    if kw in ("WHEN",):
        return "when"
    if kw in ("THEN",):
        return "then"
    return kw.lower()


def analyze_quality(req: QualityRequest) -> QualityResponse:
    steps = req.steps
    issues: list[QualityIssue] = []
    suggestions: list[str] = []

    # ── 1. Empty / no steps ───────────────────────────────────────────────
    if not steps:
        return QualityResponse(
            quality_score=0,
            quality_label="poor",
            issues=[QualityIssue(
                severity="error", step_index=None, step_text=None,
                message="Scenario has no steps.",
                why="An empty scenario cannot be executed and will always pass vacuously.",
            )],
            suggestions=["Add at least Given / When / Then steps."],
            improved_steps=[],
            best_practices=_best_practices(),
        )

    non_empty = [s for s in steps if s.text.strip()]
    if not non_empty:
        issues.append(QualityIssue(
            severity="error", step_index=None, step_text=None,
            message="All steps are empty.",
            why="Steps without text generate no executable code.",
        ))

    # ── 2. Given / When / Then structure ─────────────────────────────────
    classified = [(_classify_keyword(s.keyword), s) for s in steps if s.text.strip()]
    has_given = any(k == "given" for k, _ in classified)
    has_when  = any(k == "when"  for k, _ in classified)
    has_then  = any(k == "then"  for k, _ in classified)

    if not has_given:
        issues.append(QualityIssue(
            severity="warning", step_index=None, step_text=None,
            message="Missing GIVEN (precondition) step.",
            why="Without a Given step the initial state is undefined, making the test unreliable.",
        ))
        suggestions.append('Add a Given step to set up the initial context, e.g. "Given I navigate to the login page".')

    if not has_when:
        issues.append(QualityIssue(
            severity="error", step_index=None, step_text=None,
            message="Missing WHEN (action) step.",
            why="No user action means the scenario tests nothing.",
        ))
        suggestions.append("Add at least one WHEN step describing the user's action.")

    if not has_then:
        issues.append(QualityIssue(
            severity="error", step_index=None, step_text=None,
            message="Missing THEN (assertion) step.",
            why="Without an assertion the test always passes regardless of outcome — a classic false positive.",
        ))
        suggestions.append('Add a Then step that verifies the expected outcome, e.g. "Then I should see a success message".')

    # ── 3. Check for navigation / login precondition ──────────────────────
    step_texts = " ".join(s.text for s in steps if s.text.strip()).lower()
    has_nav  = bool(_NAV_STEP.search(step_texts))
    has_login_action = bool(_LOGIN_STEP.search(step_texts))

    if has_login_action and not has_nav:
        suggestions.append(
            'Add "Given I navigate to the login page" before login actions so the test knows where to start.'
        )

    # ── 4. Per-step analysis ──────────────────────────────────────────────
    for idx, step in enumerate(steps):
        text = step.text.strip()
        if not text:
            issues.append(QualityIssue(
                severity="warning", step_index=idx, step_text=None,
                message=f"Step {idx + 1} is empty.",
                why="Empty steps are skipped by the generator and create gaps in execution.",
            ))
            continue

        # 4a. Vague action words
        for pattern, advice in _VAGUE_ACTION_PATTERNS:
            if pattern.search(text):
                issues.append(QualityIssue(
                    severity="warning", step_index=idx, step_text=text,
                    message=f'Vague or implicit action: "{text}".',
                    why=advice,
                ))

        # 4b. Fill without quoted value
        if _VAGUE_FILL_PATTERNS.search(text) and not _QUOTED_VALUE.search(text):
            issues.append(QualityIssue(
                severity="info", step_index=idx, step_text=text,
                message=f'Step {idx + 1}: credential value not specified (use quotes).',
                why='Without a quoted value the AI cannot extract the text to type. Use: I enter "admin" in the username field.',
            ))

        # 4c. THEN steps without observable assertion keyword
        kw = _classify_keyword(step.keyword)
        if kw == "then" and not _ASSERT_KEYWORDS.search(text):
            issues.append(QualityIssue(
                severity="error", step_index=idx, step_text=text,
                message=f'Then step lacks an assertion keyword: "{text}".',
                why="Without a verification keyword the step produces no assertion — the test will always pass.",
            ))
            suggestions.append(
                f'Rewrite step {idx + 1} to start with "I should see …" or "I should be redirected to …".'
            )

    # ── 5. Score calculation ──────────────────────────────────────────────
    error_count   = sum(1 for i in issues if i.severity == "error")
    warning_count = sum(1 for i in issues if i.severity == "warning")
    info_count    = sum(1 for i in issues if i.severity == "info")

    score = 100 - (error_count * 25) - (warning_count * 10) - (info_count * 3)
    score = max(0, min(100, score))

    if score >= 75:
        label = "good"
    elif score >= 45:
        label = "medium"
    else:
        label = "poor"

    # ── 6. Improved steps ────────────────────────────────────────────────
    improved = _improve_steps(steps, has_given, has_when, has_then, has_nav)

    # ── 7. Global suggestions (deduplicated) ─────────────────────────────
    if not suggestions:
        if score == 100:
            suggestions.append("Great scenario! All steps are clear and testable.")
        else:
            suggestions.append("Review the issues above and add explicit assertions to your Then steps.")

    return QualityResponse(
        quality_score=score,
        quality_label=label,
        issues=issues,
        suggestions=list(dict.fromkeys(suggestions)),   # dedup while preserving order
        improved_steps=improved,
        best_practices=_best_practices(),
    )


def _improve_steps(
    steps: list[QualityStep],
    has_given: bool,
    has_when: bool,
    has_then: bool,
    has_nav: bool,
) -> list[ImprovedStep]:
    improved: list[ImprovedStep] = []

    # Inject missing navigation precondition
    if not has_given and not has_nav:
        improved.append(ImprovedStep(keyword="Given", text="I navigate to the application URL"))

    for step in steps:
        text = step.text.strip()
        if not text:
            continue
        kw = step.keyword
        kw_class = _classify_keyword(kw)

        # ── Drop steps whose entire content is purely vague / untestable
        # (e.g. "it works successfully", "works", "successfully").
        # These add no value and only confuse the executor.
        if _is_purely_vague(text) and kw_class != "then":
            continue

        # Fix: bare "click login" → "click on the login button"
        text = re.sub(r'\bclick\b(?!\s+on\b)\s+(.+)', r'click on the \1', text, flags=re.I)
        # Fix: "submit" → "click on the submit button"
        text = re.sub(r'^submit$', 'click on the submit button', text, flags=re.I)
        # Fix: "successfully" in Then → rewrite
        if kw_class == "then":
            text = re.sub(r'\bsuccessfully\b', '', text, flags=re.I).strip()
            text = re.sub(r'\bworks?\b', '', text, flags=re.I).strip()
            text = re.sub(r'\s{2,}', ' ', text).strip()
            # If after cleanup the Then is empty or still purely vague,
            # replace with a generic success assertion.
            if not text or _is_purely_vague(text):
                text = "I should see a confirmation or success message"
            elif not _ASSERT_KEYWORDS.search(text):
                text = f"I should see {text}"

        improved.append(ImprovedStep(keyword=kw, text=text))

    # Inject missing assertion if no Then was present
    if not has_then:
        improved.append(ImprovedStep(
            keyword="Then",
            text="I should see a confirmation or success message",
        ))

    return improved


# Words/phrases that, when they make up the *entire* meaningful content of a
# step, render the step untestable. Used to drop noise from improved scenarios.
_PURELY_VAGUE_RE = re.compile(
    r'^\s*(it|the\s+page|the\s+site|the\s+system|everything)?\s*'
    r'(works?|runs?|loads?|is\s+ok|is\s+fine|does\s+something|'
    r'successfully|successful|something)\s*'
    r'(successfully|fine|ok|well)?\s*\.?\s*$',
    re.I,
)


def _is_purely_vague(text: str) -> bool:
    """True when the step text contains nothing concrete to test."""
    return bool(_PURELY_VAGUE_RE.match(text.strip()))


def _best_practices() -> list[str]:
    return [
        'Use precise action verbs: "click on", "fill in", "select", "submit".',
        'Always add an explicit Then assertion — never rely on the absence of errors.',
        'Quote exact values in steps: I enter "admin@example.com" in the email field.',
        'Start with a Given that sets navigation context (e.g. "I navigate to /login").',
        'Avoid vague words: "works", "successfully", "something", "the page loads".',
        'One observable outcome per Then step keeps failures easy to diagnose.',
        'Use "And" to chain related steps rather than packing multiple actions in one.',
    ]


# ── Route ─────────────────────────────────────────────────────────────────

@router.post(
    "/analyze-quality",
    response_model=QualityResponse,
    summary="Analyze scenario quality",
    description=(
        "Analyzes a Gherkin scenario for quality, completeness and testability "
        "before any test execution. Returns quality score, detected issues, "
        "improvement suggestions and an improved version of the scenario."
    ),
)
async def analyze_scenario_quality(request: QualityRequest) -> QualityResponse:
    return analyze_quality(request)
