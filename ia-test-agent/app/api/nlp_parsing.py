"""
Endpoints de parsing NLP des scenarios Gherkin.
POST /api/ia/parse-scenario
POST /api/ia/parse-feature
"""

from fastapi import APIRouter, HTTPException

from app.schemas.gherkin_schemas import (
    GherkinScenarioRequest,
    GherkinParseResponse,
)
from app.services.nlp_service import NLPService

router = APIRouter(prefix="/api/ia", tags=["NLP Parsing"])

nlp_service = NLPService()


@router.post(
    "/parse-scenario",
    response_model=GherkinParseResponse,
    summary="Parser un scenario Gherkin",
    description="Analyse un scenario Gherkin et extrait les intentions "
    "et entites de chaque step via NLP.",
)
async def parse_scenario(request: GherkinScenarioRequest):
    """Parse un scenario Gherkin complet et renvoie l'analyse NLP structuree."""
    try:
        result = nlp_service.parse_scenario(request)
        return result
    except ValueError as e:
        raise HTTPException(status_code=422, detail=f"Scenario invalide : {str(e)}")
    except Exception as e:
        raise HTTPException(
            status_code=500, detail=f"Erreur interne lors du parsing : {str(e)}"
        )



