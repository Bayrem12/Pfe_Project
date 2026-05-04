"""
Endpoint d'adaptation dynamique.
POST /api/ia/adapt
"""

from fastapi import APIRouter, HTTPException

from app.schemas.test_schemas import AdaptationRequest, AdaptationResponse
from app.services.adaptation_service import AdaptationService

router = APIRouter(prefix="/api/ia", tags=["Adaptation"])

adaptation_service = AdaptationService()


@router.post(
    "/adapt",
    response_model=AdaptationResponse,
    summary="Appliquer une strategie d'adaptation dynamique",
    description="Gere les popups inattendus, changements d'interface, "
    "timeouts et erreurs d'execution.",
)
async def adapt(request: AdaptationRequest):
    """Applique une strategie d'adaptation face a un imprevu."""
    try:
        result = adaptation_service.adapt(request)
        return result
    except Exception as e:
        raise HTTPException(
            status_code=500, detail=f"Erreur lors de l'adaptation : {str(e)}"
        )
