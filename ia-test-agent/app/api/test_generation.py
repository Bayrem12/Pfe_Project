"""
Endpoint de generation automatique de tests Playwright.
POST /api/ia/generate-test
"""

from fastapi import APIRouter, HTTPException

from app.schemas.test_schemas import TestGenerationRequest, TestGenerationResponse
from app.services.generator_service import GeneratorService

router = APIRouter(prefix="/api/ia", tags=["Test Generation"])

generator_service = GeneratorService()


@router.post(
    "/generate-test",
    response_model=TestGenerationResponse,
    summary="Generer un script Playwright",
    description="Transforme la sortie du pipeline NLP + Vision "
    "en script Playwright executable.",
)
async def generate_test(request: TestGenerationRequest):
    """Genere un script de test Playwright a partir de l'analyse NLP + Vision."""
    try:
        result = generator_service.generate(request)
        return result
    except Exception as e:
        raise HTTPException(
            status_code=500, detail=f"Erreur lors de la generation : {str(e)}"
        )
