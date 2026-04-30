"""
Endpoint de reporting.
GET /api/ia/reports/{test_id}
GET /api/ia/reports/{test_id}/pipeline-trace
"""

from fastapi import APIRouter, HTTPException
from fastapi.responses import HTMLResponse

from app.schemas.report_schemas import TestReportResponse
from app.services.report_service import ReportService

router = APIRouter(prefix="/api/ia", tags=["Reporting"])

report_service = ReportService()


@router.get(
    "/reports/{test_id}",
    response_model=TestReportResponse,
    summary="Recuperer le rapport d'un test execute",
    description="Renvoie le rapport detaille d'un test avec captures d'ecran et metriques.",
)
async def get_report(test_id: str):
    """Recupere le rapport d'un test par son identifiant."""
    try:
        result = report_service.get_report(test_id)
        if result is None:
            raise HTTPException(status_code=404, detail=f"Rapport {test_id} introuvable")
        return result
    except HTTPException:
        raise
    except Exception as e:
        raise HTTPException(
            status_code=500, detail=f"Erreur lors de la recuperation du rapport : {str(e)}"
        )


@router.get(
    "/reports/{test_id}/pipeline-trace",
    response_class=HTMLResponse,
    summary="Trace HTML du pipeline pour un test",
    description="Retourne la page HTML de trace d'exécution NLP→Génération→Exécution.",
)
async def get_pipeline_trace_html(test_id: str):
    """Retourne le HTML de trace pipeline pour un test."""
    import os
    from app.config import settings

    html_path = os.path.join(settings.REPORTS_DIR, f"pipeline_trace_{test_id}.html")
    if not os.path.isfile(html_path):
        # Try generating on the fly if trace data is in memory
        trace = report_service.get_pipeline_trace(test_id)
        if trace is None:
            raise HTTPException(
                status_code=404,
                detail=f"Trace pipeline {test_id} introuvable. "
                       "Lancez d'abord le pipeline via POST /api/ia/pipeline."
            )
        html_path = report_service.generate_pipeline_html(test_id) or ""
        if not html_path or not os.path.isfile(html_path):
            raise HTTPException(status_code=500, detail="Erreur de génération de la trace HTML.")

    with open(html_path, encoding="utf-8") as f:
        return HTMLResponse(content=f.read())

