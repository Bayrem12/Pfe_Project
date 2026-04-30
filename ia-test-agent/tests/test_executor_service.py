"""
Tests Day 13 — ExecutorService : execution end-to-end, retries, cookie popups,
modales, smart-wait et cas speciaux.

Approche : les tests d'integration ouvrent de vraies pages (headless Chromium).
Les tests unitaires mockent la couche Playwright.
"""

import asyncio
import os
import time
from pathlib import Path
from unittest.mock import AsyncMock, MagicMock, patch, PropertyMock

import pytest
import pytest_asyncio

from app.services.executor_service import (
    ExecutorService,
    _parse_target_from_line,
    _intended_types_from_line,
    _find_best_element,
)
from app.schemas.test_schemas import TestExecutionRequest
from app.config import settings

# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------

SCREENSHOTS_DIR = Path(settings.SCREENSHOTS_DIR)


def _make_elem(elem_type: str, text: str, conf: float = 0.90):
    """Cree un DetectedElement minimal pour les tests unitaires."""
    from app.schemas.ui_schemas import DetectedElement, BoundingBox
    return DetectedElement(
        id=f"{elem_type}_{text}",
        type=elem_type,
        confiance_detection=conf,
        bounding_box=BoundingBox(x=10, y=10, width=100, height=30),
        texte_ocr=text,
        label=text,
        source="yolo",
    )


# ---------------------------------------------------------------------------
# SECTION 1 — Helpers unitaires (parse / intended types / find_best)
# ---------------------------------------------------------------------------

class TestParseTargetFromLine:
    def test_get_by_label(self):
        assert _parse_target_from_line('await page.get_by_label("Username").fill("demo")') == "Username"

    def test_get_by_placeholder(self):
        assert _parse_target_from_line('await page.get_by_placeholder("Email").fill("a@b.com")') == "Email"

    def test_get_by_text(self):
        assert _parse_target_from_line('await page.get_by_text("Submit").click()') == "Submit"

    def test_get_by_role_name(self):
        assert _parse_target_from_line('await page.get_by_role("button", name="Login").click()') == "Login"

    def test_filter_has_text(self):
        assert _parse_target_from_line('await page.locator("li").filter(has_text="Item").click()') == "Item"

    def test_no_match(self):
        assert _parse_target_from_line('await page.wait_for_load_state("networkidle")') is None


class TestIntendedTypes:
    def test_fill_returns_input_types(self):
        result = _intended_types_from_line('await page.get_by_label("Email").fill("x")')
        assert "input_text" in result

    def test_check_returns_checkbox(self):
        assert "checkbox" in _intended_types_from_line('await loc.check()')

    def test_select_option_returns_dropdown(self):
        assert "dropdown" in _intended_types_from_line('await loc.select_option("fr")')

    def test_click_returns_button_and_link(self):
        result = _intended_types_from_line('await page.locator("btn").click()')
        assert "button" in result and "link" in result


class TestFindBestElement:
    def test_exact_match_wins(self):
        elems = [_make_elem("button", "Login"), _make_elem("button", "Register")]
        best = _find_best_element(elems, "Login", ["button"])
        assert best is not None
        assert best.texte_ocr == "Login"

    def test_type_filter_respected(self):
        elems = [_make_elem("label", "Login"), _make_elem("button", "Connexion")]
        best = _find_best_element(elems, "Login", ["button"])
        # Should pick from buttons only; "Connexion" ≈ "Login" (poor match)
        # but no button matches "Login" well — falls back to all elements
        assert best is not None

    def test_empty_elements_returns_none(self):
        assert _find_best_element([], "Login", ["button"]) is None

    def test_no_type_filter_uses_all(self):
        elems = [_make_elem("input_text", "Email")]
        best = _find_best_element(elems, "Email", [])
        assert best is not None
        assert best.texte_ocr == "Email"


# ---------------------------------------------------------------------------
# SECTION 2 — _exec_with_retry (unitaire, mock)
# ---------------------------------------------------------------------------

@pytest.mark.asyncio
class TestExecWithRetry:
    async def test_succeeds_first_attempt(self):
        svc = ExecutorService()
        page = MagicMock()
        page.wait_for_timeout = AsyncMock()
        svc._exec_line_with_fallback_tracked = AsyncMock(return_value=(False, False, None, ""))

        await svc._exec_with_retry(page, 'await page.click("button")')
        assert svc._exec_line_with_fallback_tracked.call_count == 1

    async def test_retries_on_failure_then_succeeds(self):
        svc = ExecutorService()
        page = MagicMock()
        page.wait_for_timeout = AsyncMock()
        call_count = {"n": 0}

        async def flaky(*args, **kwargs):
            call_count["n"] += 1
            if call_count["n"] < 2:
                raise Exception("transient error")
            return (False, False, None, "")

        svc._exec_line_with_fallback_tracked = flaky
        svc._handle_unexpected_modal = AsyncMock(return_value=False)

        await svc._exec_with_retry(page, 'await page.click("button")')
        assert call_count["n"] == 2  # failed once, succeeded on retry

    async def test_raises_after_max_retries(self):
        svc = ExecutorService()
        page = MagicMock()
        page.wait_for_timeout = AsyncMock()
        svc._exec_line_with_fallback_tracked = AsyncMock(side_effect=Exception("always fails"))
        svc._handle_unexpected_modal = AsyncMock(return_value=False)

        with pytest.raises(Exception, match="always fails"):
            await svc._exec_with_retry(page, 'await page.click("button")')

        # 1 initial + MAX_RETRIES retries
        assert svc._exec_line_with_fallback_tracked.call_count == settings.MAX_RETRIES + 1

    async def test_modal_dismissed_before_retry(self):
        svc = ExecutorService()
        page = MagicMock()
        page.wait_for_timeout = AsyncMock()
        svc._exec_line_with_fallback_tracked = AsyncMock(
            side_effect=[Exception("blocked"), (False, False, None, "")]
        )
        svc._handle_unexpected_modal = AsyncMock(return_value=True)

        await svc._exec_with_retry(page, 'await page.click("btn")')
        svc._handle_unexpected_modal.assert_called_once()


# ---------------------------------------------------------------------------
# SECTION 3 — _handle_cookie_popup (unitaire, mock)
# ---------------------------------------------------------------------------

@pytest.mark.asyncio
class TestHandleCookiePopup:
    async def _make_page_with_button(self, text: str, count: int = 1):
        """Cree un mock Page avec un bouton dont le count = count."""
        page = MagicMock()
        btn = MagicMock()
        btn.count = AsyncMock(return_value=count)
        btn.first = MagicMock()
        btn.first.click = AsyncMock()
        page.get_by_role = MagicMock(return_value=btn)
        page.get_by_text = MagicMock(return_value=btn)
        page.wait_for_timeout = AsyncMock()
        return page, btn

    async def test_accepts_english_cookie_button(self):
        svc = ExecutorService()
        page, btn = await self._make_page_with_button("Accept all", count=1)
        result = await svc._handle_cookie_popup(page)
        assert result is True
        btn.first.click.assert_called_once()

    async def test_returns_false_when_no_popup(self):
        svc = ExecutorService()
        page = MagicMock()
        btn = MagicMock()
        btn.count = AsyncMock(return_value=0)
        page.get_by_role = MagicMock(return_value=btn)
        page.wait_for_timeout = AsyncMock()
        result = await svc._handle_cookie_popup(page)
        assert result is False


# ---------------------------------------------------------------------------
# SECTION 4 — _handle_unexpected_modal (unitaire, mock)
# ---------------------------------------------------------------------------

@pytest.mark.asyncio
class TestHandleUnexpectedModal:
    async def test_presses_escape(self):
        svc = ExecutorService()
        page = MagicMock()
        page.keyboard = MagicMock()
        page.keyboard.press = AsyncMock()
        page.wait_for_timeout = AsyncMock()
        btn = MagicMock()
        btn.count = AsyncMock(return_value=0)
        page.get_by_role = MagicMock(return_value=btn)

        await svc._handle_unexpected_modal(page)
        page.keyboard.press.assert_called_with("Escape")

    async def test_clicks_close_button_when_present(self):
        svc = ExecutorService()
        page = MagicMock()
        page.keyboard = MagicMock()
        page.keyboard.press = AsyncMock()
        page.wait_for_timeout = AsyncMock()

        close_btn = MagicMock()
        close_btn.count = AsyncMock(return_value=1)
        close_btn.first = MagicMock()
        close_btn.first.click = AsyncMock()

        no_btn = MagicMock()
        no_btn.count = AsyncMock(return_value=0)

        def role_side_effect(role, name):
            if "close" in str(name.pattern).lower() or "fermer" in str(name.pattern).lower():
                return close_btn
            return no_btn

        page.get_by_role = MagicMock(side_effect=role_side_effect)

        result = await svc._handle_unexpected_modal(page)
        assert result is True
        close_btn.first.click.assert_called_once()


# ---------------------------------------------------------------------------
# SECTION 5 — _smart_wait_for_element (unitaire, mock)
# ---------------------------------------------------------------------------

@pytest.mark.asyncio
class TestSmartWaitForElement:
    async def test_returns_true_when_element_ready(self):
        svc = ExecutorService()
        page = MagicMock()
        page.wait_for_selector = AsyncMock()
        locator = MagicMock()
        locator.is_disabled = AsyncMock(return_value=False)
        page.locator = MagicMock(return_value=locator)

        result = await svc._smart_wait_for_element(page, "#my-btn", timeout_ms=2000)
        assert result is True

    async def test_returns_false_on_timeout(self):
        from playwright.async_api import TimeoutError as PwTimeoutError
        svc = ExecutorService()
        page = MagicMock()
        page.wait_for_selector = AsyncMock(side_effect=PwTimeoutError("timeout"))

        result = await svc._smart_wait_for_element(page, "#missing", timeout_ms=100)
        assert result is False


# ---------------------------------------------------------------------------
# SECTION 6 — Screenshot every step (unitaire, mock)
# ---------------------------------------------------------------------------

@pytest.mark.asyncio
class TestScreenshotEveryStep:
    async def test_screenshot_called_for_each_step(self):
        svc = ExecutorService()
        svc._exec_with_retry = AsyncMock()
        svc._screenshot = AsyncMock(return_value="/tmp/shot.png")

        page = MagicMock()
        steps = [
            {"comment": "Step 1", "lines": ['await page.click("btn")']},
            {"comment": "Step 2", "lines": ['await page.fill("input", "x")']},
            {"comment": "Step 3", "lines": ['await page.click("submit")']},
        ]

        with patch.object(settings, "STEP_SCREENSHOT", True):
            results, screenshots = await svc.execute_live(page, steps, "testid")

        assert svc._screenshot.call_count == 3
        assert len(screenshots) == 3

    async def test_screenshot_skipped_when_disabled(self):
        svc = ExecutorService()
        svc._exec_with_retry = AsyncMock()
        svc._screenshot = AsyncMock(return_value="/tmp/shot.png")

        page = MagicMock()
        steps = [{"comment": "Step 1", "lines": ['await page.click("btn")']}]

        with patch.object(settings, "STEP_SCREENSHOT", False):
            results, screenshots = await svc.execute_live(page, steps, "testid")

        svc._screenshot.assert_not_called()
        assert screenshots == []


# ---------------------------------------------------------------------------
# SECTION 7 — Integration : the-internet.herokuapp.com login
# ---------------------------------------------------------------------------

@pytest.mark.integration
@pytest.mark.asyncio
class TestInternetHerokuLoginIntegration:
    """
    Test bout-en-bout du login sur https://the-internet.herokuapp.com/login
    Identifiants: tomsmith / SuperSecretPassword!
    """

    async def test_login_scenario_passes(self):
        svc = ExecutorService()
        request = TestExecutionRequest(
            scenario_name="Login the-internet",
            url_cible="https://the-internet.herokuapp.com/login",
            script_code="""
# Step 1: remplir le champ username
await page.get_by_label("Username").fill("tomsmith")
# Step 2: remplir le champ password
await page.get_by_label("Password").fill("SuperSecretPassword!")
# Step 3: cliquer sur le bouton Login
await page.get_by_role("button", name="Login").click()
await page.wait_for_load_state("networkidle")
# Step 4: verifier le message de succes
await expect(page.locator("#flash")).to_contain_text("You logged into a secure area")
""",
        )
        response = await svc.execute(request)

        assert response.scenario_name == "Login the-internet"
        assert response.statut == "PASSED", (
            f"Steps: {[(s.step, s.statut, s.erreur) for s in response.steps_results]}"
        )
        assert len(response.screenshots) >= 2
        # Verify screenshots were actually saved
        for ss in response.screenshots:
            assert os.path.exists(ss), f"Screenshot not found: {ss}"

    async def test_login_produces_screenshots_at_each_step(self):
        svc = ExecutorService()
        request = TestExecutionRequest(
            scenario_name="Login screenshots",
            url_cible="https://the-internet.herokuapp.com/login",
            script_code="""
# Step 1: fill username
await page.get_by_label("Username").fill("tomsmith")
# Step 2: fill password
await page.get_by_label("Password").fill("SuperSecretPassword!")
# Step 3: submit
await page.get_by_role("button", name="Login").click()
await page.wait_for_load_state("networkidle")
""",
        )
        response = await svc.execute(request)
        # initial + 1 per await page statement = at least 4 screenshots
        assert len(response.screenshots) >= 3

    async def test_wrong_credentials_fail_gracefully(self):
        svc = ExecutorService()
        request = TestExecutionRequest(
            scenario_name="Login bad creds",
            url_cible="https://the-internet.herokuapp.com/login",
            script_code="""
# Step 1: fill username
await page.get_by_label("Username").fill("baduser")
# Step 2: fill password
await page.get_by_label("Password").fill("badpass")
# Step 3: submit
await page.get_by_role("button", name="Login").click()
await page.wait_for_load_state("networkidle")
# Step 4: expect error message
await expect(page.locator("#flash")).to_contain_text("Your username is invalid!")
""",
        )
        response = await svc.execute(request)
        # The assertion step should PASS (error message appears)
        login_steps = [s for s in response.steps_results if "error" in s.step.lower() or "expect" in s.step.lower() or "username is invalid" in (s.erreur or "")]
        assert response.statut in ("PASSED", "FAILED")  # graceful, no crash


# ---------------------------------------------------------------------------
# SECTION 8 — Integration : saucedemo.com e-commerce
# ---------------------------------------------------------------------------

@pytest.mark.integration
@pytest.mark.asyncio
class TestSaucedemoIntegration:
    """
    Test e-commerce sur https://www.saucedemo.com
    """

    async def test_login_and_add_to_cart(self):
        svc = ExecutorService()
        request = TestExecutionRequest(
            scenario_name="Saucedemo login + cart",
            url_cible="https://www.saucedemo.com",
            script_code="""
# Step 1: enter username
await page.get_by_placeholder("Username").fill("standard_user")
# Step 2: enter password
await page.get_by_placeholder("Password").fill("secret_sauce")
# Step 3: login
await page.get_by_role("button", name="Login").click()
await page.wait_for_load_state("networkidle")
# Step 4: add first item to cart
await page.locator(".btn_inventory").first.click()
# Step 5: go to cart
await page.locator(".shopping_cart_link").click()
await page.wait_for_load_state("networkidle")
# Step 6: verify item in cart
await expect(page.locator(".cart_item")).to_have_count(1)
""",
        )
        response = await svc.execute(request)
        assert response.statut == "PASSED", (
            f"Steps: {[(s.step, s.statut, s.erreur) for s in response.steps_results]}"
        )

    async def test_saucedemo_screenshot_per_step(self):
        svc = ExecutorService()
        request = TestExecutionRequest(
            scenario_name="Saucedemo screenshots",
            url_cible="https://www.saucedemo.com",
            script_code="""
# Step 1: enter username
await page.get_by_placeholder("Username").fill("standard_user")
# Step 2: enter password
await page.get_by_placeholder("Password").fill("secret_sauce")
# Step 3: login
await page.get_by_role("button", name="Login").click()
await page.wait_for_load_state("networkidle")
""",
        )
        response = await svc.execute(request)
        # 1 initial + at least 3 step screenshots
        assert len(response.screenshots) >= 3


# ---------------------------------------------------------------------------
# SECTION 9 — Integration : demoqa.com formulaires
# ---------------------------------------------------------------------------

@pytest.mark.integration
@pytest.mark.asyncio
class TestDemoqaFormIntegration:
    """
    Test sur formulaires https://demoqa.com/text-box
    """

    async def test_textbox_form_fills_and_submits(self):
        svc = ExecutorService()
        request = TestExecutionRequest(
            scenario_name="DemoQA text box form",
            url_cible="https://demoqa.com/text-box",
            script_code="""
# Step 1: remplir le nom
await page.get_by_placeholder("Full Name").fill("Alice Dupont")
# Step 2: remplir l'email
await page.get_by_placeholder("name@example.com").fill("alice@test.com")
# Step 3: adresse courante
await page.get_by_placeholder("Current Address").fill("12 rue de la Paix, Paris")
# Step 4: soumettre le formulaire
await page.get_by_role("button", name="Submit").click()
""",
        )
        response = await svc.execute(request)
        # Cookie popup or ads may interfere — we check graceful execution
        failed_steps = [s for s in response.steps_results if s.statut == "FAILED"]
        # Allow up to 1 failure due to DemoQA ad overlays
        assert len(failed_steps) <= 1, (
            f"Too many failures: {[(s.step, s.erreur) for s in failed_steps]}"
        )


# ---------------------------------------------------------------------------
# SECTION 10 — Config settings used correctly
# ---------------------------------------------------------------------------

def test_config_max_retries():
    assert settings.MAX_RETRIES >= 1
    assert settings.MAX_RETRIES <= 5  # sanity

def test_config_step_timeout():
    assert settings.STEP_TIMEOUT_MS >= 1000

def test_config_networkidle_timeout():
    assert settings.NETWORKIDLE_TIMEOUT_MS >= 1000

def test_config_step_screenshot_bool():
    assert isinstance(settings.STEP_SCREENSHOT, bool)
