"""
Schemas Pydantic pour les rapports de test.
"""

from pydantic import BaseModel, Field

from app.schemas.test_schemas import StepResult, FallbackTrace


class TestReportResponse(BaseModel):
    """Rapport complet d'un test execute."""

    test_id: str
    scenario_name: str
    statut: str
    duree_ms: float
    steps_results: list[StepResult] = []
    screenshots: list[str] = []
    date_execution: str = ""
    url_cible: str = ""
    navigateur: str = "chromium"


# ---------------------------------------------------------------------------
# Pipeline trace schemas
# ---------------------------------------------------------------------------

class StepTraceInfo(BaseModel):
    """Trace complète d'un step dans le pipeline : NLP → Génération → Exécution."""

    step_num: int
    keyword: str = ""               # Given / When / Then / And / But
    gherkin_text: str = ""          # texte original du step Gherkin
    intention: str = ""             # intention NLP détectée (naviguer, cliquer…)
    entites: list[dict] = []        # entités NLP: [{nom, valeur, type}]
    generated_code: list[str] = []  # lignes Playwright générées pour ce step
    statut: str = ""                # OK / FAILED
    duree_ms: float = 0.0
    erreur: str = ""
    erreur_type: str = ""           # ELEMENT_NOT_FOUND / TIMEOUT / ASSERTION_FAILED / NETWORK
    erreur_explication: str = ""    # explication humaine de l'erreur
    method_used: str = ""           # playwright_direct / visual_fallback / retry_adaptation
    retry_count: int = 0
    visual_fallback_used: bool = False
    playwright_error: str = ""
    fallback_trace: FallbackTrace | None = None


class PipelineTraceData(BaseModel):
    """Données complètes du pipeline pour la vue de diagnostic."""

    test_id: str
    scenario_name: str
    statut: str
    duree_ms: float
    url_cible: str = ""
    language: str = "fr"
    date_generated: str = ""
    steps: list[StepTraceInfo] = []
    recovery_stats: dict = {}
    screenshots: list[str] = []
