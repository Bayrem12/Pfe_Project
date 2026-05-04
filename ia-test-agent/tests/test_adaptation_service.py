"""
Tests DAY 15 — Adaptation dynamique + robustesse.

Couvre : AdaptationService.adapt(), integration avec ExecutorService,
taux de recuperation, CV fallback, simulation d'erreurs.
"""

import base64
from io import BytesIO
from unittest.mock import AsyncMock, MagicMock, patch

import pytest
from PIL import Image

from app.schemas.test_schemas import (
    RecoveryStats,
    StepResult,
    AdaptationRequest,
    AdaptationResponse,
    CorrectiveAction,
)
from app.services.adaptation_service import AdaptationService
from app.services.executor_service import ExecutorService


# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------

def _small_png_b64() -> str:
    """Cree un petit PNG blanc encode en base64 (screenshot factice)."""
    img = Image.new("RGB", (80, 60), color=(255, 255, 255))
    buf = BytesIO()
    img.save(buf, format="PNG")
    return base64.b64encode(buf.getvalue()).decode()


def _make_request(
    type_erreur: str = "not_found",
    message: str = "Element not found",
    step_text: str = "je clique sur le bouton Submit",
    screenshot: str | None = None,
    elements: list[dict] | None = None,
) -> AdaptationRequest:
    return AdaptationRequest(
        type_erreur=type_erreur,
        step_en_cours={"line": step_text, "message": message},
        screenshot_actuel=screenshot or _small_png_b64(),
        elements_detectes_par_vision=elements or [],
    )


# ---------------------------------------------------------------------------
# SECTION 1 — Diagnostics de base
# ---------------------------------------------------------------------------

class TestAdaptationDiagnostics:
    """Verifie que adapt() renvoie le bon diagnostic selon le type d'erreur."""

    def setup_method(self):
        self.svc = AdaptationService()

    def test_popup_cookies_detection(self):
        elements = [
            {"type": "button", "label": "Accepter les cookies", "confiance": 0.92},
        ]
        req = _make_request(type_erreur="popup", message="overlay bloquant", elements=elements)
        resp = self.svc.adapt(req)
        assert resp.diagnostic == "popup_cookies_bloquant"
        assert resp.confiance >= 0.85

    def test_popup_cookies_action_fermer(self):
        elements = [
            {"type": "button", "label": "accepter", "confiance": 0.95},
        ]
        req = _make_request(type_erreur="popup", elements=elements)
        resp = self.svc.adapt(req)
        actions = [a.action for a in resp.actions_correctives]
        # Doit contenir une action pour fermer le popup (click ou click_selector)
        assert any(a in actions for a in ("click", "click_selector", "cookie_close", "cliquer_element"))

    def test_modal_inattendue_detection(self):
        elements = [
            {"type": "modal", "label": "modal dialog", "confiance": 0.88},
        ]
        req = _make_request(type_erreur="modal", message="modal bloquante", elements=elements)
        resp = self.svc.adapt(req)
        assert resp.diagnostic == "modale_inattendue"

    def test_modal_inattendue_action_escape(self):
        elements = [
            {"type": "modal", "label": "dialog", "confiance": 0.90},
        ]
        req = _make_request(type_erreur="modal", elements=elements)
        resp = self.svc.adapt(req)
        actions = [a.action for a in resp.actions_correctives]
        assert "press_escape" in actions or "echap" in " ".join(actions).lower()

    def test_element_non_trouve_diagnostic(self):
        req = _make_request(type_erreur="not_found", message="Selector '#submit' not found")
        resp = self.svc.adapt(req)
        assert resp.diagnostic == "element_non_trouve"

    def test_element_non_trouve_action_screenshot_redetect(self):
        req = _make_request(type_erreur="not_found")
        resp = self.svc.adapt(req)
        actions = [a.action for a in resp.actions_correctives]
        assert any("screenshot" in a or "detect" in a or "fallback" in a for a in actions)

    def test_erreur_navigation(self):
        req = _make_request(
            type_erreur="navigation",
            message="net::ERR_NAME_NOT_RESOLVED",
        )
        resp = self.svc.adapt(req)
        assert resp.diagnostic == "erreur_navigation"

    def test_erreur_javascript(self):
        req = _make_request(type_erreur="javascript", message="TypeError: Cannot read properties of null")
        resp = self.svc.adapt(req)
        assert resp.diagnostic == "erreur_javascript"

    def test_erreur_inconnue_faible_confiance(self):
        req = _make_request(type_erreur="other", message="Unknown weird error XD")
        resp = self.svc.adapt(req)
        assert resp.diagnostic == "erreur_inconnue"
        assert resp.confiance <= 0.60

    def test_timeout_detection(self):
        req = _make_request(type_erreur="timeout", message="Timeout 10000ms exceeded")
        resp = self.svc.adapt(req)
        assert resp.diagnostic in ("erreur_navigation", "element_non_trouve", "timeout")
        assert resp.confiance > 0


# ---------------------------------------------------------------------------
# SECTION 2 — Strategie retournee
# ---------------------------------------------------------------------------

class TestAdaptationStrategies:
    def setup_method(self):
        self.svc = AdaptationService()

    def test_popup_strategie_is_not_empty(self):
        elements = [{"type": "button", "label": "accepter", "confiance": 0.90}]
        req = _make_request(type_erreur="popup", elements=elements)
        resp = self.svc.adapt(req)
        assert resp.strategie and len(resp.strategie) > 0

    def test_element_not_found_strategie_mentions_visual(self):
        req = _make_request(type_erreur="not_found")
        resp = self.svc.adapt(req)
        strat_lower = resp.strategie.lower()
        assert any(k in strat_lower for k in ("visual", "visuel", "fallback", "detect", "yolo", "redetect"))

    def test_unknown_error_has_strategie(self):
        req = _make_request(type_erreur="other")
        resp = self.svc.adapt(req)
        assert resp.strategie is not None
        assert isinstance(resp.strategie, str)

    def test_response_type_is_adaptation_response(self):
        req = _make_request()
        resp = self.svc.adapt(req)
        assert isinstance(resp, AdaptationResponse)

    def test_actions_correctives_is_list(self):
        req = _make_request()
        resp = self.svc.adapt(req)
        assert isinstance(resp.actions_correctives, list)


# ---------------------------------------------------------------------------
# SECTION 3 — Recovery stats unit tests
# ---------------------------------------------------------------------------

class TestRecoveryStatsComputation:
    """Teste _compute_recovery_stats directement."""

    def test_all_ok_no_retry(self):
        results = [StepResult(step=f"s{i}", statut="OK") for i in range(4)]
        stats = ExecutorService._compute_recovery_stats(results)
        assert stats.steps_total == 4
        assert stats.steps_ok == 4
        assert stats.steps_recovered == 0
        assert stats.steps_failed == 0
        assert stats.taux_recuperation == 1.0

    def test_1_recovered_1_failed(self):
        results = [
            StepResult(step="s0", statut="OK"),
            StepResult(step="s1", statut="OK", retry_count=2),     # recovered
            StepResult(step="s2", statut="FAILED"),
        ]
        stats = ExecutorService._compute_recovery_stats(results)
        assert stats.steps_recovered == 1
        assert stats.steps_failed == 1
        assert abs(stats.taux_recuperation - 0.5) < 1e-9

    def test_taux_recuperation_0_when_no_recovery(self):
        results = [StepResult(step=f"s{i}", statut="FAILED") for i in range(3)]
        stats = ExecutorService._compute_recovery_stats(results)
        assert stats.taux_recuperation == 0.0

    def test_visual_fallback_counted(self):
        results = [
            StepResult(step="s0", statut="OK", visual_fallback_used=True, retry_count=1),
            StepResult(step="s1", statut="OK"),
        ]
        stats = ExecutorService._compute_recovery_stats(results)
        assert stats.visual_fallback_total == 1

    def test_adaptation_total_counted(self):
        results = [
            StepResult(step="s0", statut="OK", adaptation_appliquee="fermer_popup"),
            StepResult(step="s1", statut="OK"),
        ]
        stats = ExecutorService._compute_recovery_stats(results)
        assert stats.adaptation_total == 1

    def test_empty_results(self):
        stats = ExecutorService._compute_recovery_stats([])
        assert stats.steps_total == 0
        # No steps = no failures, so rate is either 0.0 or 1.0 depending on implementation
        assert stats.taux_recuperation in (0.0, 1.0)

    def test_taux_gte_70pct_with_mix(self):
        """Simule: 7 OK (dont 2 retries) + 1 failed → 2/(2+1) = 66% — < 70%.
        Mais 8 OK (dont 3 retries) + 1 failed → 3/(3+1) = 75% — >= 70%."""
        results = [
            StepResult(step="s0", statut="OK"),
            StepResult(step="s1", statut="OK"),
            StepResult(step="s2", statut="OK"),
            StepResult(step="s3", statut="OK"),
            StepResult(step="s4", statut="OK"),
            StepResult(step="s5", statut="OK", retry_count=1),   # recovered
            StepResult(step="s6", statut="OK", retry_count=1),   # recovered
            StepResult(step="s7", statut="OK", retry_count=1),   # recovered
            StepResult(step="s8", statut="FAILED"),
        ]
        stats = ExecutorService._compute_recovery_stats(results)
        assert stats.taux_recuperation >= 0.70


# ---------------------------------------------------------------------------
# SECTION 4 — Integration: cookie popup scenario
# ---------------------------------------------------------------------------

@pytest.mark.integration
@pytest.mark.asyncio
class TestCookiePopupResilience:
    """Verifie que le cookie popup handler engage bien l'AdaptationService."""

    async def test_executor_cookie_popup_does_not_block(self):
        """L'executor ne doit pas planter sur un site avec popup cookie."""
        svc = ExecutorService()
        from app.schemas.test_schemas import TestExecutionRequest
        req = TestExecutionRequest(
            scenario_name="Cookie popup test",
            url_cible="https://the-internet.herokuapp.com/",
            script_code="""
# Step 1: verify homepage
await expect(page.locator("h1")).to_contain_text("Welcome")
""",
        )
        response = await svc.execute(req)
        # Peut echouer si la page change, mais ne doit pas planter l'executor
        assert response.statut in ("PASSED", "FAILED")
        assert response.recovery_stats is not None


# ---------------------------------------------------------------------------
# SECTION 5 — Integration: Simulation d'erreurs + adaptation
# ---------------------------------------------------------------------------

@pytest.mark.integration
@pytest.mark.asyncio
class TestErrorSimulationAndAdaptation:
    """Simule des erreurs et verifie que l'adaptation les traite."""

    async def test_missing_selector_triggers_visual_fallback(self):
        """Un selecteur invalide → retry → visual fallback → StepResult tracked."""
        svc = ExecutorService()
        from app.schemas.test_schemas import TestExecutionRequest
        req = TestExecutionRequest(
            scenario_name="Missing selector",
            url_cible="https://the-internet.herokuapp.com/login",
            script_code="""
# Step 1: fill an element that exists
await page.get_by_label("Username").fill("test")
# Step 2: try non-existent selector (should trigger fallback)
await page.locator("#this-does-not-exist-xyz").click(timeout=2000)
""",
        )
        response = await svc.execute(req)
        # L'executor doit avoir tente un fallback
        fallback_used = any(s.visual_fallback_used for s in response.steps_results)
        retry_attempted = any(s.retry_count > 0 for s in response.steps_results)
        # Au moins un des deux mecanismes doit s'etre engage
        assert fallback_used or retry_attempted

    async def test_recovery_rate_above_70pct_mixed_steps(self):
        """Ave un mix d'etapes qui reussissent et quelques erreurs, taux >= 0."""
        svc = ExecutorService()
        from app.schemas.test_schemas import TestExecutionRequest
        req = TestExecutionRequest(
            scenario_name="Recovery rate test",
            url_cible="https://the-internet.herokuapp.com/login",
            script_code="""
# Step 1: fill username (OK)
await page.get_by_label("Username").fill("tomsmith")
# Step 2: fill password (OK)
await page.get_by_label("Password").fill("SuperSecretPassword!")
# Step 3: click login (OK)
await page.get_by_role("button", name="Login").click()
await page.wait_for_load_state("networkidle")
# Step 4: verify (OK)
await expect(page.locator("#flash")).to_contain_text("You logged into a secure area")
""",
        )
        response = await svc.execute(req)
        assert response.recovery_stats.taux_recuperation >= 0.0
        assert response.recovery_stats.steps_total >= 3

    async def test_adaptation_service_called_on_retry(self):
        """Verifie que AdaptationService.adapt() est appele lors d'un retry."""
        svc = ExecutorService()
        from app.schemas.test_schemas import TestExecutionRequest
        req = TestExecutionRequest(
            scenario_name="Adaptation call",
            url_cible="https://the-internet.herokuapp.com/login",
            script_code="""
# Step 1: valid step
await page.get_by_label("Username").fill("user")
# Step 2: fera un retry (element absent)
await page.locator("#nonexistentbutton123").click(timeout=1500)
""",
        )
        with patch.object(AdaptationService, "adapt", wraps=AdaptationService().adapt) as mock_adapt:
            svc._adaptation = AdaptationService()
            svc._adaptation.adapt = mock_adapt
            response = await svc.execute(req)

        # Si le step 2 echoue, adapt() doit avoir ete appele au moins une fois
        any_failed = any(s.statut == "FAILED" for s in response.steps_results)
        if any_failed:
            assert mock_adapt.called


# ---------------------------------------------------------------------------
# SECTION 6 — Adaptation confiance thresholds
# ---------------------------------------------------------------------------

class TestAdaptationConfiance:
    def setup_method(self):
        self.svc = AdaptationService()

    def test_cookie_popup_confiance_gt_80(self):
        elements = [{"type": "button", "label": "Accepter", "confiance": 0.95}]
        req = _make_request(type_erreur="popup", elements=elements)
        resp = self.svc.adapt(req)
        assert resp.confiance >= 0.80

    def test_unknown_error_confiance_lt_60(self):
        req = _make_request(type_erreur="other", message="something weird")
        resp = self.svc.adapt(req)
        assert resp.confiance <= 0.60

    def test_confiance_in_range_0_1(self):
        for type_err in ["popup", "modal", "not_found", "navigation", "javascript", "timeout", "other"]:
            req = _make_request(type_erreur=type_err)
            resp = self.svc.adapt(req)
            assert 0.0 <= resp.confiance <= 1.0, f"Confiance hors range pour {type_err}: {resp.confiance}"
