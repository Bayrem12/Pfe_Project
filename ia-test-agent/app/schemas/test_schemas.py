"""
Schemas Pydantic pour la generation et l'execution de tests.
"""

from typing import Optional
from pydantic import BaseModel, Field

from app.schemas.gherkin_schemas import ParsedStep


# --- Fallback diagnostic trace (Day 16+) ----------------------------------

class DetectionCandidate(BaseModel):
    """One element considered during the visual fallback."""

    rank: int = 0
    type: str = ""
    label: str = ""
    source: str = "yolo"             # 'yolo' | 'dom' | 'contour'
    confiance_detection: float = 0.0
    similarity_score: float = 0.0
    bbox_x: int = 0
    bbox_y: int = 0
    bbox_w: int = 0
    bbox_h: int = 0
    texte_ocr: str = ""
    was_chosen: bool = False


class FallbackTrace(BaseModel):
    """Full diagnostic trace of one visual fallback invocation."""

    target_text: str = ""
    type_filter: list[str] = Field(default_factory=list)
    yolo_count: int = 0
    dom_count: int = 0
    merged_count: int = 0
    candidates: list[DetectionCandidate] = Field(default_factory=list)
    winner_rank: int = -1
    winner_source: str = ""
    winner_coords_x: int = 0
    winner_coords_y: int = 0
    action_dispatched: str = ""      # 'fill' | 'click' | 'check' | ...
    success: bool = False
    screenshot_raw: str = ""
    screenshot_annotated: str = ""


# --- Generation ---


class ActionMapping(BaseModel):
    """User-defined override that pins a Gherkin pattern to a concrete selector + action."""

    intent_pattern: str = Field(..., description="Regex applied to the step raw_text")
    action_type: str = Field(..., description="click | type | select | navigate | assert | hover | scroll | wait")
    selector_strategy: str = Field("css", description="css | xpath | text | role | testid")
    selector_value: str = Field("", description="Selector body (e.g. '#login-btn' or 'Connexion')")
    description: str = ""
    priority: int = Field(
        0,
        description="Higher value = checked first. Use to ensure more-specific patterns "
        "take precedence over broader ones (e.g. 'login button' at 10 beats 'login' at 0).",
    )


class GenerationRequest(BaseModel):
    """Requete de generation d'un script de test."""

    scenario_name: str
    url_cible: str = ""
    steps: list[ParsedStep] = Field(..., description="Steps analyses (sortie NLP + mapping)")
    action_mappings: list[ActionMapping] = Field(
        default_factory=list,
        description="Optional project-level overrides: when a step's raw_text matches "
        "intent_pattern, the generator uses the mapping's selector + action instead of NLP.",
    )


# Alias for backward compat
TestGenerationRequest = GenerationRequest


class TestGenerationResponse(BaseModel):
    """Reponse de generation de script."""

    scenario_name: str
    script_filename: str
    script_code: str


# --- Execution ---


class TestExecutionRequest(BaseModel):
    """Requete d'execution d'un test."""

    scenario_name: str
    script_code: str = ""
    script_filename: str = ""
    url_cible: str = ""
    # Optional override of the global HEADLESS setting. When None, the value
    # configured in app settings is used. When False, the browser window is
    # visible so the user can watch the test execute.
    headless: bool | None = None


class StepResult(BaseModel):
    """Resultat d'un step individuel."""

    step: str
    statut: str = "OK"
    duree_ms: float = 0
    erreur: str = ""
    # Day 15 — tracking de robustesse
    retry_count: int = 0          # nombre de tentatives avant succes/echec
    visual_fallback_used: bool = False  # True si la detection YOLO+OCR a pris le relai
    adaptation_appliquee: str = ""      # strategie d'adaptation utilisee (ex: "handle_popup_then_retry")
    # Day 16 — diagnostic trace (only set when visual fallback ran)
    playwright_error: str = ""          # original locator error before fallback
    fallback_trace: Optional[FallbackTrace] = None


class RecoveryStats(BaseModel):
    """Statistiques de recuperation apres erreur (Day 15)."""

    steps_total: int = 0
    steps_ok: int = 0                # OK des le premier essai
    steps_recovered: int = 0         # echec puis recupere (retry/fallback/adaptation)
    steps_failed: int = 0            # echec definitif
    taux_recuperation: float = 0.0   # steps_recovered / (steps_recovered + steps_failed)
    visual_fallback_total: int = 0   # total de fallbacks visuels utilises
    adaptation_total: int = 0        # total de strategies d'adaptation appliquees


class TestExecutionResponse(BaseModel):
    """Reponse d'execution d'un test."""

    test_id: str
    scenario_name: str
    statut: str
    duree_ms: float
    steps_results: list[StepResult]
    screenshots: list[str] = []
    recovery_stats: RecoveryStats = RecoveryStats()


# --- Adaptation dynamique ---


class AdaptationRequest(BaseModel):
    """Requete d'adaptation dynamique."""

    type_erreur: str
    step_en_cours: dict = {}
    screenshot_actuel: str = ""
    elements_detectes_par_vision: list[dict] = []


class CorrectiveAction(BaseModel):
    """Action corrective proposee."""

    action: str
    selector: str = ""
    raison: str = ""


class AdaptationResponse(BaseModel):
    """Reponse d'adaptation dynamique."""

    diagnostic: str
    actions_correctives: list[CorrectiveAction] = []
    strategie: str
    confiance: float
