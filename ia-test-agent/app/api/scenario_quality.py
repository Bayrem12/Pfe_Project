"""
POST /api/ia/analyze-quality  — thin API adapter.
All business logic lives in app/services/scenario_quality_service.py.
"""

from __future__ import annotations

from fastapi import APIRouter
from pydantic import BaseModel

from app.services.scenario_quality_service import (
    StepInput,
    analyze_quality,
)

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
    severity: str           # "error" | "warning" | "info"
    step_index: int | None
    step_text: str | None
    message: str
    why: str


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
    using_llm: bool = False
    analysis_method: str = "regex"       # regex | semantic-ai | zero-shot-ai | llm-ai


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
    result = analyze_quality(
        scenario_name=request.scenario_name,
        steps=[StepInput(keyword=s.keyword, text=s.text) for s in request.steps],
        language=request.language,
    )
    return QualityResponse(
        quality_score=result.quality_score,
        quality_label=result.quality_label,
        issues=[QualityIssue(**vars(i)) for i in result.issues],
        suggestions=result.suggestions,
        improved_steps=[ImprovedStep(**vars(s)) for s in result.improved_steps],
        best_practices=result.best_practices,
        using_llm=result.using_llm,
        analysis_method=result.analysis_method,
    )


