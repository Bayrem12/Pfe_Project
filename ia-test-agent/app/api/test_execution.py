"""
Endpoint d'execution automatique de tests.
POST /api/ia/execute-test
"""

from fastapi import APIRouter, HTTPException

from app.schemas.test_schemas import TestExecutionRequest, TestExecutionResponse
from app.services.executor_service import ExecutorService

router = APIRouter(prefix="/api/ia", tags=["Test Execution"])

executor_service = ExecutorService()


@router.post(
    "/execute-test",
    response_model=TestExecutionResponse,
    summary="Executer un script de test genere",
    description="Execute un script Playwright genere et renvoie les resultats.",
)
async def execute_test(request: TestExecutionRequest):
    """Execute un test genere et renvoie le resultat."""
    try:
        result = await executor_service.execute(request)
        return result
    except Exception as e:
        raise HTTPException(
            status_code=500, detail=f"Erreur lors de l'execution : {str(e)}"
        )
