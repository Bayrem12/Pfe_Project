"""
Endpoint d'analyse de panne (AI Failure Analysis).

POST /api/ia/analyze-failure
  Donne un verdict structure (categorie, cause racine, explication, fix
  suggere) pour un step echoue donne.

Permet de re-analyser un test apres coup, ou d'analyser une panne fournie
par l'exterieur sans rejouer tout le pipeline.
"""

from fastapi import APIRouter
from pydantic import BaseModel, Field

from app.services.failure_analysis_service import analyze_step_failure


router = APIRouter(prefix="/api/ia", tags=["Failure Analysis"])


class AnalyzeFailureRequest(BaseModel):
    step_text: str = Field(..., description="Gherkin step text that failed")
    error_message: str = Field(..., description="Raw Playwright/executor error")
    selector: str = Field("", description="Selector that was used (if any)")
    keyword: str = Field("", description="Gherkin keyword (Given/When/Then/...)")
    visual_fallback_used: bool = False
    retry_count: int = 0


class AnalyzeFailureResponse(BaseModel):
    category: str
    root_cause: str
    title: str
    explanation: str
    where: str
    is_test_issue: bool
    suggested_fix: str
    confidence: float
    analysis_method: str = "regex"


@router.post("/analyze-failure", response_model=AnalyzeFailureResponse)
async def analyze_failure(req: AnalyzeFailureRequest) -> AnalyzeFailureResponse:
    """Run the 4-stage ML cascade failure analyzer on a single failed step."""
    analysis = analyze_step_failure(
        step_text=req.step_text,
        error_message=req.error_message,
        selector=req.selector,
        keyword=req.keyword,
        visual_fallback_used=req.visual_fallback_used,
        retry_count=req.retry_count,
    )
    return AnalyzeFailureResponse(**analysis.to_dict())
