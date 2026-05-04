"""
Endpoint de health check du service IA.
"""

from fastapi import APIRouter

router = APIRouter(prefix="/api/ia", tags=["Health"])


@router.get("/health", summary="Verifier l'etat du service IA")
async def health_check():
    """Verifie que le service IA est operationnel."""
    return {
        "status": "ok",
        "service": "ia-test-agent",
        "version": "0.1.0",
    }
