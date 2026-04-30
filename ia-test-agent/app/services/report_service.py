"""
Service de generation de rapports de test.
Genere des rapports JSON + HTML avec captures d'ecran et metriques.
"""

import os
import re
import logging
from datetime import datetime
from typing import Optional

from jinja2 import Environment, FileSystemLoader

from app.schemas.report_schemas import TestReportResponse, PipelineTraceData
from app.schemas.test_schemas import TestExecutionResponse, StepResult
from app.config import settings

logger = logging.getLogger(__name__)

_TEMPLATE_DIR = os.path.join(os.path.dirname(__file__), "..", "templates")
_env = Environment(loader=FileSystemLoader(os.path.abspath(_TEMPLATE_DIR)))


def _classify_error(erreur: str) -> tuple[str, str]:
    """Returns (erreur_type, erreur_explication) for a step error message."""
    if not erreur:
        return "", ""
    err = erreur.lower()
    if "err_name_not_resolved" in err or "err_connection" in err or "err_internet" in err:
        return "ERREUR RÉSEAU", "Impossible de joindre le site. Vérifiez l'URL et votre connexion internet."
    if "timeout" in err and any(k in err for k in ("get_by_role", "get_by_label", "locator", "get_by_placeholder", "check", "fill")):
        return "ÉLÉMENT INTROUVABLE", (
            "Le sélecteur généré par l'IA ne correspond à aucun élément sur la page. "
            "Le label Gherkin ne correspond pas à la structure HTML réelle du site. "
            "Le fallback visuel YOLO+DOM a été tenté."
        )
    if "timeout" in err and any(k in err for k in ("networkidle", "load_state")):
        return "TIMEOUT PAGE", "La page prend trop de temps à charger. Le site est peut-être lent ou inaccessible."
    if "timeout" in err:
        return "TIMEOUT", "L'opération a dépassé le délai configuré."
    if any(k in err for k in ("expected to contain text", "to_contain_text", "unexpected value")):
        return "ASSERTION ÉCHOUÉE", (
            "Le texte attendu n'est pas présent sur la page. "
            "Cause probable : une action précédente a échoué (champ non rempli, "
            "mauvais identifiants, navigation vers la mauvaise page)."
        )
    if "to_be_visible" in err:
        return "ASSERTION ÉCHOUÉE", "L'élément attendu n'est pas visible sur la page."
    return "ERREUR", "Erreur lors de l'exécution du step."


def _classify_method(statut: str, visual_fallback_used: bool, retry_count: int, adaptation: str) -> str:
    """Returns a method_used key."""
    if statut == "FAILED" and visual_fallback_used:
        return "visual_fallback_tried"
    if statut == "FAILED":
        return "direct_failed"
    if visual_fallback_used:
        return "visual_fallback"
    if retry_count > 0 or adaptation:
        return "retry_adaptation"
    return "playwright_direct"


def _compute_recovery_stats(steps: list[StepResult]) -> dict:
    total = len(steps)
    ok = sum(1 for s in steps if s.statut == "OK" and s.retry_count == 0 and not s.visual_fallback_used)
    recovered = sum(1 for s in steps if s.statut == "OK" and (s.retry_count > 0 or s.visual_fallback_used))
    failed = sum(1 for s in steps if s.statut == "FAILED")
    visual_total = sum(1 for s in steps if s.visual_fallback_used)
    adapt_total = sum(1 for s in steps if s.adaptation_appliquee)
    denominator = recovered + failed
    taux = round(recovered / denominator, 3) if denominator > 0 else 1.0
    return {
        "steps_total": total,
        "steps_ok": ok,
        "steps_recovered": recovered,
        "steps_failed": failed,
        "taux_recuperation": taux,
        "visual_fallback_total": visual_total,
        "adaptation_total": adapt_total,
    }


class ReportService:
    """Service de generation et recuperation de rapports."""

    # Class-level storage so all instances share the same data
    _reports: dict[str, TestReportResponse] = {}
    _pipeline_traces: dict[str, PipelineTraceData] = {}

    # ------------------------------------------------------------------
    # CRUD — reports
    # ------------------------------------------------------------------

    def get_report(self, test_id: str) -> Optional[TestReportResponse]:
        return ReportService._reports.get(test_id)

    def list_reports(self) -> list[TestReportResponse]:
        return list(ReportService._reports.values())

    def save_report(self, report: TestReportResponse) -> None:
        ReportService._reports[report.test_id] = report

    # ------------------------------------------------------------------
    # CRUD — pipeline traces
    # ------------------------------------------------------------------

    def store_pipeline_trace(self, trace: PipelineTraceData) -> None:
        ReportService._pipeline_traces[trace.test_id] = trace

    def get_pipeline_trace(self, test_id: str) -> Optional[PipelineTraceData]:
        return ReportService._pipeline_traces.get(test_id)

    # ------------------------------------------------------------------
    # Build from execution
    # ------------------------------------------------------------------

    def build_from_execution(
        self,
        execution: TestExecutionResponse,
        url_cible: str = "",
    ) -> TestReportResponse:
        report = TestReportResponse(
            test_id=execution.test_id,
            scenario_name=execution.scenario_name,
            statut=execution.statut,
            duree_ms=execution.duree_ms,
            steps_results=execution.steps_results,
            screenshots=execution.screenshots,
            date_execution=datetime.now().isoformat(),
            url_cible=url_cible,
            navigateur=settings.BROWSER_TYPE,
        )
        self.save_report(report)
        return report

    # ------------------------------------------------------------------
    # HTML report generation
    # ------------------------------------------------------------------

    def generate_html(self, test_id: str) -> Optional[str]:
        """Genere le rapport HTML enrichi et le sauvegarde sur disque."""
        report = self.get_report(test_id)
        if report is None:
            return None

        os.makedirs(settings.REPORTS_DIR, exist_ok=True)

        recovery_stats = _compute_recovery_stats(report.steps_results)
        pipeline_trace_exists = test_id in ReportService._pipeline_traces

        try:
            template = _env.get_template("report.html.j2")
            html = template.render(
                scenario_name=report.scenario_name,
                date_execution=report.date_execution,
                duree_ms=report.duree_ms,
                statut=report.statut,
                steps_results=[s.model_dump() for s in report.steps_results],
                screenshots=report.screenshots,
                url_cible=report.url_cible,
                navigateur=report.navigateur,
                test_id=report.test_id,
                recovery_stats=recovery_stats,
                pipeline_trace_exists=pipeline_trace_exists,
            )
        except Exception as e:
            logger.error("Failed to render HTML report: %s", e)
            return None

        filepath = os.path.join(settings.REPORTS_DIR, f"report_{test_id}.html")
        with open(filepath, "w", encoding="utf-8") as f:
            f.write(html)

        logger.info("HTML report saved to %s", filepath)
        return filepath

    # ------------------------------------------------------------------
    # Pipeline trace HTML generation
    # ------------------------------------------------------------------

    def generate_pipeline_html(self, test_id: str) -> Optional[str]:
        """Genere le HTML de trace pipeline et le sauvegarde sur disque."""
        trace = self.get_pipeline_trace(test_id)
        if trace is None:
            return None

        os.makedirs(settings.REPORTS_DIR, exist_ok=True)

        try:
            template = _env.get_template("pipeline_trace.html.j2")
            html = template.render(
                test_id=trace.test_id,
                scenario_name=trace.scenario_name,
                statut=trace.statut,
                duree_ms=trace.duree_ms,
                url_cible=trace.url_cible,
                language=trace.language,
                date_generated=trace.date_generated,
                steps=[s.model_dump() for s in trace.steps],
                recovery_stats=trace.recovery_stats,
                screenshots=trace.screenshots,
            )
        except Exception as e:
            logger.error("Failed to render pipeline trace HTML: %s", e)
            return None

        filepath = os.path.join(settings.REPORTS_DIR, f"pipeline_trace_{test_id}.html")
        with open(filepath, "w", encoding="utf-8") as f:
            f.write(html)

        logger.info("Pipeline trace saved to %s", filepath)
        return filepath

    # ------------------------------------------------------------------
    # Technical trace HTML — vision-aware diagnostic view (Day 16)
    # ------------------------------------------------------------------

    def generate_technical_trace_html(self, test_id: str) -> Optional[str]:
        """
        Genere un rapport HTML technique riche montrant pour chaque step :
          - le code Playwright execute,
          - si le fallback visuel a tourne,
          - les detections YOLO + DOM avec scores de similarite,
          - le screenshot brut + la version annotee cote a cote.
        """
        trace = self.get_pipeline_trace(test_id)
        if trace is None:
            return None

        os.makedirs(settings.REPORTS_DIR, exist_ok=True)

        # Convert steps to dicts so the template doesn't have to know Pydantic
        steps_dump: list[dict] = []
        steps_with_fallback = 0
        steps_failed = 0
        for s in trace.steps:
            d = s.model_dump()
            if d.get("fallback_trace"):
                steps_with_fallback += 1
            if d.get("statut") == "FAILED":
                steps_failed += 1
            steps_dump.append(d)

        try:
            template = _env.get_template("technical_trace.html.j2")
            html = template.render(
                test_id=trace.test_id,
                scenario_name=trace.scenario_name,
                statut=trace.statut,
                duree_ms=trace.duree_ms,
                url_cible=trace.url_cible,
                language=trace.language,
                date_generated=trace.date_generated,
                steps=steps_dump,
                recovery_stats=trace.recovery_stats,
                steps_with_fallback=steps_with_fallback,
                steps_failed=steps_failed,
                steps_total=len(steps_dump),
            )
        except Exception as e:
            logger.error("Failed to render technical trace HTML: %s", e)
            return None

        filepath = os.path.join(settings.REPORTS_DIR, f"technical_trace_{test_id}.html")
        with open(filepath, "w", encoding="utf-8") as f:
            f.write(html)

        logger.info("Technical trace saved to %s", filepath)
        return filepath

