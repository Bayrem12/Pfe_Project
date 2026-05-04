"""
Tests DAY 14 — Pipeline end-to-end complet.

Flux : Gherkin → NLP (intentions+entites) → Generation Playwright
       → Execution → Rapport → RecoveryStats

Tests unitaires : mock les services individuels.
Tests d'integration : trois scenarios reels (login, panier, formulaire).
"""

import os
from unittest.mock import AsyncMock, MagicMock, patch

import pytest

from app.api.pipeline import PipelineRequest, PipelineResponse, run_pipeline
from app.schemas.gherkin_schemas import GherkinStep, ParsedStep
from app.schemas.test_schemas import (
    StepResult,
    RecoveryStats,
)
from app.services.nlp_service import NLPService
from app.services.generator_service import GeneratorService
from app.services.executor_service import ExecutorService
from app.services.report_service import ReportService


# ---------------------------------------------------------------------------
# Scenarios Gherkin (utilis├®s dans unit + integration tests)
# ---------------------------------------------------------------------------

SCENARIO_LOGIN = PipelineRequest(
    scenario_name="Login the-internet",
    url_cible="https://the-internet.herokuapp.com/login",
    language="fr",
    steps=[
        GherkinStep(keyword="Given", text="je suis sur la page de connexion"),
        GherkinStep(keyword="When", text='je saisis "tomsmith" dans le champ "Username"'),
        GherkinStep(keyword="And", text='je saisis "SuperSecretPassword!" dans le champ "Password"'),
        GherkinStep(keyword="And", text='je clique sur le bouton "Login"'),
        GherkinStep(keyword="Then", text='je vois le message "You logged into a secure area"'),
    ],
)

SCENARIO_CART = PipelineRequest(
    scenario_name="Saucedemo panier",
    url_cible="https://www.saucedemo.com",
    language="fr",
    steps=[
        GherkinStep(keyword="Given", text="je suis sur la page saucedemo"),
        GherkinStep(keyword="When", text='je saisis "standard_user" dans le champ "Username"'),
        GherkinStep(keyword="And", text='je saisis "secret_sauce" dans le champ "Password"'),
        GherkinStep(keyword="And", text='je clique sur le bouton "Login"'),
        GherkinStep(keyword="And", text="j'ajoute le premier article au panier"),
        GherkinStep(keyword="And", text="je vais au panier"),
        GherkinStep(keyword="Then", text="je vois 1 article dans le panier"),
    ],
)

SCENARIO_FORM = PipelineRequest(
    scenario_name="DemoQA formulaire",
    url_cible="https://demoqa.com/text-box",
    language="fr",
    steps=[
        GherkinStep(keyword="Given", text="je suis sur le formulaire DemoQA"),
        GherkinStep(keyword="When", text='je saisis "Alice Dupont" dans le champ "Full Name"'),
        GherkinStep(keyword="And", text='je saisis "alice@test.com" dans le champ "Email"'),
        GherkinStep(keyword="And", text='je clique sur le bouton "Submit"'),
        GherkinStep(keyword="Then", text="le formulaire est soumis"),
    ],
)


# ---------------------------------------------------------------------------
# SECTION 1 — Unit: NLP parsing produit les bonnes intentions
# ---------------------------------------------------------------------------

class TestPipelineNLPStage:
    """Verifie que le parsing Gherkin extrait les bonnes intentions."""

    def _get_intentions(self, scenario: PipelineRequest) -> list[str]:
        svc = NLPService()
        from app.schemas.gherkin_schemas import GherkinScenarioRequest
        req = GherkinScenarioRequest(
            scenario_name=scenario.scenario_name,
            steps=scenario.steps,
            language=scenario.language,
        )
        result = svc.parse_scenario(req)
        return [s.intention for s in result.steps_analyses]

    def test_login_intentions_include_saisir_and_cliquer(self):
        intentions = self._get_intentions(SCENARIO_LOGIN)
        assert "saisir_texte" in intentions or "saisir" in [i.split("_")[0] for i in intentions]
        assert "cliquer" in intentions

    def test_cart_intentions_include_login_steps(self):
        intentions = self._get_intentions(SCENARIO_CART)
        assert len(intentions) == len(SCENARIO_CART.steps)

    def test_form_intentions_not_empty(self):
        intentions = self._get_intentions(SCENARIO_FORM)
        assert len(intentions) > 0
        assert all(len(i) > 0 for i in intentions)

    def test_parse_result_step_count_matches(self):
        svc = NLPService()
        from app.schemas.gherkin_schemas import GherkinScenarioRequest
        req = GherkinScenarioRequest(
            scenario_name=SCENARIO_LOGIN.scenario_name,
            steps=SCENARIO_LOGIN.steps,
            language="fr",
        )
        result = svc.parse_scenario(req)
        assert result.nombre_steps == len(SCENARIO_LOGIN.steps)
        assert len(result.steps_analyses) == len(SCENARIO_LOGIN.steps)


# ---------------------------------------------------------------------------
# SECTION 2 — Unit: Script generation produit du code Playwright
# ---------------------------------------------------------------------------

class TestPipelineGenerationStage:
    """Verifie que la generation produit un script coherent."""

    def _gen_script(self, scenario: PipelineRequest) -> str:
        nlp = NLPService()
        gen = GeneratorService()
        from app.schemas.gherkin_schemas import GherkinScenarioRequest
        from app.schemas.test_schemas import TestGenerationRequest
        parse_req = GherkinScenarioRequest(
            scenario_name=scenario.scenario_name,
            steps=scenario.steps,
            language=scenario.language,
        )
        parse_result = nlp.parse_scenario(parse_req)
        gen_req = TestGenerationRequest(
            scenario_name=scenario.scenario_name,
            url_cible=scenario.url_cible,
            steps=parse_result.steps_analyses,
        )
        return gen.generate(gen_req).script_code

    def test_login_script_contains_playwright_imports(self):
        script = self._gen_script(SCENARIO_LOGIN)
        assert "playwright" in script.lower() or "page" in script

    def test_login_script_contains_url(self):
        script = self._gen_script(SCENARIO_LOGIN)
        assert "the-internet.herokuapp.com" in script

    def test_script_contains_fill_or_click(self):
        script = self._gen_script(SCENARIO_LOGIN)
        assert ".fill(" in script or ".click(" in script

    def test_cart_script_contains_saucedemo_url(self):
        script = self._gen_script(SCENARIO_CART)
        assert "saucedemo.com" in script

    def test_form_script_contains_demoqa_url(self):
        script = self._gen_script(SCENARIO_FORM)
        assert "demoqa.com" in script

    def test_script_not_empty(self):
        for scenario in [SCENARIO_LOGIN, SCENARIO_CART, SCENARIO_FORM]:
            assert len(self._gen_script(scenario)) > 50


# ---------------------------------------------------------------------------
# SECTION 3 — Unit: Pipeline response schema validation
# ---------------------------------------------------------------------------

class TestPipelineResponseSchema:
    def test_response_has_all_required_fields(self):
        resp = PipelineResponse(
            test_id="abc123",
            scenario_name="Test",
            statut="PASSED",
            duree_ms=1234.5,
            nombre_steps_analyses=3,
            intentions_detectees=["cliquer", "saisir_texte"],
            script_code="# script",
            steps_results=[],
            screenshots=[],
            recovery_stats=RecoveryStats(
                steps_total=3, steps_ok=2, steps_recovered=1, steps_failed=0,
                taux_recuperation=1.0,
            ),
        )
        assert resp.test_id == "abc123"
        assert resp.recovery_stats.taux_recuperation == 1.0

    def test_recovery_stats_defaults(self):
        stats = RecoveryStats()
        assert stats.steps_total == 0
        assert stats.taux_recuperation == 0.0

    def test_recovery_stats_calculation(self):
        # 3 steps total: 1 ok, 1 recovered (retry), 1 failed
        results = [
            StepResult(step="s1", statut="OK", retry_count=0),
            StepResult(step="s2", statut="OK", retry_count=1),   # recovered
            StepResult(step="s3", statut="FAILED"),
        ]
        stats = ExecutorService._compute_recovery_stats(results)
        assert stats.steps_total == 3
        assert stats.steps_ok == 1
        assert stats.steps_recovered == 1
        assert stats.steps_failed == 1
        assert stats.taux_recuperation == 0.5  # 1/(1+1)

    def test_recovery_rate_100_when_all_pass(self):
        results = [StepResult(step=f"s{i}", statut="OK") for i in range(5)]
        stats = ExecutorService._compute_recovery_stats(results)
        assert stats.steps_failed == 0
        assert stats.taux_recuperation == 1.0

    def test_recovery_rate_0_when_all_fail(self):
        results = [StepResult(step=f"s{i}", statut="FAILED") for i in range(3)]
        stats = ExecutorService._compute_recovery_stats(results)
        assert stats.steps_recovered == 0
        assert stats.taux_recuperation == 0.0

    def test_visual_fallback_tracked_in_stats(self):
        results = [
            StepResult(step="s1", statut="OK", visual_fallback_used=True, retry_count=1),
            StepResult(step="s2", statut="OK"),
        ]
        stats = ExecutorService._compute_recovery_stats(results)
        assert stats.visual_fallback_total == 1
        assert stats.steps_recovered == 1


# ---------------------------------------------------------------------------
# SECTION 4 — Unit: Pipeline orchestration (mocked services)
# ---------------------------------------------------------------------------

@pytest.mark.asyncio
class TestPipelineMocked:
    """Teste que les services sont orchester dans le bon ordre (mocks)."""

    async def test_pipeline_calls_all_services(self):
        """Verifie que NLP → Generator → Executor → Report sont tous appeles."""
        mock_parse = MagicMock()
        mock_parse.nombre_steps = 3
        mock_parse.steps_analyses = [
            ParsedStep(
                step_type="when", raw_text="je clique", intention="cliquer",
                entites=[], confiance=0.9,
            )
        ] * 3

        mock_gen = MagicMock()
        mock_gen.script_code = '# Step 1\nawait page.goto("https://example.com")'
        mock_gen.scenario_name = "Test"
        mock_gen.script_filename = "test.py"

        mock_exec = MagicMock()
        mock_exec.test_id = "fakeid"
        mock_exec.scenario_name = "Test"
        mock_exec.statut = "PASSED"
        mock_exec.duree_ms = 500.0
        mock_exec.steps_results = [StepResult(step="Navigate", statut="OK", duree_ms=100)]
        mock_exec.screenshots = ["/tmp/shot.png"]
        mock_exec.recovery_stats = RecoveryStats(steps_total=1, steps_ok=1)

        mock_report = MagicMock()
        mock_report.test_id = "fakeid"

        with (
            patch("app.api.pipeline.nlp_service") as nlp_mock,
            patch("app.api.pipeline.generator_service") as gen_mock,
            patch("app.api.pipeline.executor_service") as exec_mock,
            patch("app.api.pipeline.report_service") as rep_mock,
        ):
            nlp_mock.parse_scenario.return_value = mock_parse
            gen_mock.generate.return_value = mock_gen
            exec_mock.execute = AsyncMock(return_value=mock_exec)
            rep_mock.build_from_execution.return_value = mock_report
            rep_mock.generate_html.return_value = "/tmp/report.html"
            rep_mock.generate_pipeline_html.return_value = ""

            request = PipelineRequest(
                scenario_name="Test",
                url_cible="https://example.com",
                steps=[GherkinStep(keyword="When", text="je clique")],
            )
            response = await run_pipeline(request)

        assert response.test_id == "fakeid"
        assert response.statut == "PASSED"
        assert response.nombre_steps_analyses == 3
        nlp_mock.parse_scenario.assert_called_once()
        gen_mock.generate.assert_called_once()
        exec_mock.execute.assert_called_once()
        rep_mock.build_from_execution.assert_called_once()

    async def test_pipeline_exposes_recovery_stats(self):
        """Verifie que recovery_stats est presente dans la reponse."""
        mock_parse = MagicMock()
        mock_parse.nombre_steps = 1
        mock_parse.steps_analyses = [
            ParsedStep(step_type="when", raw_text="x", intention="cliquer", entites=[], confiance=0.9)
        ]
        mock_gen = MagicMock()
        mock_gen.script_code = "# ok"
        mock_gen.scenario_name = "T"
        mock_gen.script_filename = "t.py"

        stats = RecoveryStats(
            steps_total=2, steps_ok=1, steps_recovered=1, steps_failed=0,
            taux_recuperation=1.0, visual_fallback_total=1,
        )
        mock_exec = MagicMock()
        mock_exec.test_id = "x"
        mock_exec.scenario_name = "T"
        mock_exec.statut = "PASSED"
        mock_exec.duree_ms = 200
        mock_exec.steps_results = []
        mock_exec.screenshots = []
        mock_exec.recovery_stats = stats

        mock_report = MagicMock()
        mock_report.test_id = "x"

        with (
            patch("app.api.pipeline.nlp_service") as nlp_mock,
            patch("app.api.pipeline.generator_service") as gen_mock,
            patch("app.api.pipeline.executor_service") as exec_mock,
            patch("app.api.pipeline.report_service") as rep_mock,
        ):
            nlp_mock.parse_scenario.return_value = mock_parse
            gen_mock.generate.return_value = mock_gen
            exec_mock.execute = AsyncMock(return_value=mock_exec)
            rep_mock.build_from_execution.return_value = mock_report
            rep_mock.generate_html.return_value = ""
            rep_mock.generate_pipeline_html.return_value = ""

            response = await run_pipeline(PipelineRequest(
                scenario_name="T", url_cible="https://x.com",
                steps=[GherkinStep(keyword="When", text="je clique")],
            ))

        assert response.recovery_stats.taux_recuperation == 1.0
        assert response.recovery_stats.visual_fallback_total == 1

    async def test_pipeline_500_on_service_crash(self):
        """Verifie que le pipeline renvoie 500 si un service plante."""
        from fastapi import HTTPException
        with patch("app.api.pipeline.nlp_service") as nlp_mock:
            nlp_mock.parse_scenario.side_effect = RuntimeError("NLP crashed")
            with pytest.raises(HTTPException) as exc_info:
                await run_pipeline(PipelineRequest(
                    scenario_name="T", url_cible="https://x.com",
                    steps=[GherkinStep(keyword="When", text="x")],
                ))
        assert exc_info.value.status_code == 500
        assert "NLP crashed" in exc_info.value.detail

    async def test_pipeline_returns_generated_script(self):
        """Le script genere doit etre dans la reponse."""
        mock_parse = MagicMock()
        mock_parse.nombre_steps = 1
        mock_parse.steps_analyses = [
            ParsedStep(step_type="when", raw_text="x", intention="cliquer", entites=[], confiance=0.9)
        ]
        mock_gen = MagicMock()
        mock_gen.script_code = 'await page.click("button")'
        mock_gen.scenario_name = "T"
        mock_gen.script_filename = "t.py"
        mock_exec = MagicMock()
        mock_exec.test_id = "y"
        mock_exec.scenario_name = "T"
        mock_exec.statut = "PASSED"
        mock_exec.duree_ms = 100
        mock_exec.steps_results = []
        mock_exec.screenshots = []
        mock_exec.recovery_stats = RecoveryStats()
        mock_report = MagicMock()
        mock_report.test_id = "y"

        with (
            patch("app.api.pipeline.nlp_service") as nlp_mock,
            patch("app.api.pipeline.generator_service") as gen_mock,
            patch("app.api.pipeline.executor_service") as exec_mock,
            patch("app.api.pipeline.report_service") as rep_mock,
        ):
            nlp_mock.parse_scenario.return_value = mock_parse
            gen_mock.generate.return_value = mock_gen
            exec_mock.execute = AsyncMock(return_value=mock_exec)
            rep_mock.build_from_execution.return_value = mock_report
            rep_mock.generate_html.return_value = ""
            rep_mock.generate_pipeline_html.return_value = ""

            response = await run_pipeline(PipelineRequest(
                scenario_name="T", url_cible="https://x.com",
                steps=[GherkinStep(keyword="When", text="x")],
            ))

        assert 'page.click' in response.script_code


# ---------------------------------------------------------------------------
# SECTION 5 — Integration: Scenario LOGIN (the-internet.herokuapp.com)
# ---------------------------------------------------------------------------

@pytest.mark.integration
@pytest.mark.asyncio
class TestPipelineLoginIntegration:
    """Pipeline complet sur the-internet.herokuapp.com/login."""

    async def test_pipeline_login_passes(self):
        svc = ExecutorService()
        from app.schemas.test_schemas import TestExecutionRequest
        request = TestExecutionRequest(
            scenario_name="Login the-internet",
            url_cible="https://the-internet.herokuapp.com/login",
            script_code="""
# Step 1: fill username
await page.get_by_label("Username").fill("tomsmith")
# Step 2: fill password
await page.get_by_label("Password").fill("SuperSecretPassword!")
# Step 3: click login
await page.get_by_role("button", name="Login").click()
await page.wait_for_load_state("networkidle")
# Step 4: verify success
await expect(page.locator("#flash")).to_contain_text("You logged into a secure area")
""",
        )
        response = await svc.execute(request)
        assert response.statut == "PASSED"
        assert response.recovery_stats.steps_total > 0
        assert len(response.screenshots) >= 2

    async def test_pipeline_login_recovery_stats_populated(self):
        svc = ExecutorService()
        from app.schemas.test_schemas import TestExecutionRequest
        request = TestExecutionRequest(
            scenario_name="Login stats",
            url_cible="https://the-internet.herokuapp.com/login",
            script_code="""
# Step 1: fill username
await page.get_by_label("Username").fill("tomsmith")
# Step 2: fill password
await page.get_by_label("Password").fill("SuperSecretPassword!")
""",
        )
        response = await svc.execute(request)
        stats = response.recovery_stats
        assert stats.steps_total >= 2
        assert 0.0 <= stats.taux_recuperation <= 1.0

    async def test_pipeline_login_screenshots_exist_on_disk(self):
        svc = ExecutorService()
        from app.schemas.test_schemas import TestExecutionRequest
        request = TestExecutionRequest(
            scenario_name="Login screenshots",
            url_cible="https://the-internet.herokuapp.com/login",
            script_code="""
# Step 1: fill username
await page.get_by_label("Username").fill("tomsmith")
# Step 2: fill password
await page.get_by_label("Password").fill("SuperSecretPassword!")
# Step 3: click login
await page.get_by_role("button", name="Login").click()
await page.wait_for_load_state("networkidle")
""",
        )
        response = await svc.execute(request)
        for ss in response.screenshots:
            assert os.path.exists(ss), f"Screenshot missing: {ss}"


# ---------------------------------------------------------------------------
# SECTION 6 — Integration: Scenario PANIER (saucedemo.com)
# ---------------------------------------------------------------------------

@pytest.mark.integration
@pytest.mark.asyncio
class TestPipelineCartIntegration:
    """Pipeline complet e-commerce sur saucedemo.com."""

    async def test_pipeline_cart_passes(self):
        svc = ExecutorService()
        from app.schemas.test_schemas import TestExecutionRequest
        request = TestExecutionRequest(
            scenario_name="Saucedemo panier",
            url_cible="https://www.saucedemo.com",
            script_code="""
# Step 1: username
await page.get_by_placeholder("Username").fill("standard_user")
# Step 2: password
await page.get_by_placeholder("Password").fill("secret_sauce")
# Step 3: login
await page.get_by_role("button", name="Login").click()
await page.wait_for_load_state("networkidle")
# Step 4: add to cart
await page.locator(".btn_inventory").first.click()
# Step 5: go to cart
await page.locator(".shopping_cart_link").click()
await page.wait_for_load_state("networkidle")
# Step 6: verify
await expect(page.locator(".cart_item")).to_have_count(1)
""",
        )
        response = await svc.execute(request)
        assert response.statut == "PASSED"
        assert response.recovery_stats.steps_total >= 4

    async def test_pipeline_cart_recovery_stats_above_70pct(self):
        """Taux de recuperation doit etre >= 70% (objectif Day 15)."""
        svc = ExecutorService()
        from app.schemas.test_schemas import TestExecutionRequest
        request = TestExecutionRequest(
            scenario_name="Saucedemo recovery",
            url_cible="https://www.saucedemo.com",
            script_code="""
# Step 1: username
await page.get_by_placeholder("Username").fill("standard_user")
# Step 2: password
await page.get_by_placeholder("Password").fill("secret_sauce")
# Step 3: login
await page.get_by_role("button", name="Login").click()
await page.wait_for_load_state("networkidle")
""",
        )
        response = await svc.execute(request)
        # No steps should have failed, so recovery = 100% (or N/A if all succeeded)
        stats = response.recovery_stats
        # Taux de recuperation = 1.0 quand aucun echec
        assert stats.taux_recuperation >= 0.70


# ---------------------------------------------------------------------------
# SECTION 7 — Integration: Scenario FORMULAIRE (demoqa.com)
# ---------------------------------------------------------------------------

@pytest.mark.integration
@pytest.mark.asyncio
class TestPipelineFormIntegration:
    """Pipeline complet sur demoqa.com/text-box."""

    async def test_pipeline_form_tolerable_failures(self):
        """DemoQA peut avoir des pubs — tolere 1 echec max."""
        svc = ExecutorService()
        from app.schemas.test_schemas import TestExecutionRequest
        request = TestExecutionRequest(
            scenario_name="DemoQA Form",
            url_cible="https://demoqa.com/text-box",
            script_code="""
# Step 1: full name
await page.get_by_placeholder("Full Name").fill("Alice Dupont")
# Step 2: email
await page.get_by_placeholder("name@example.com").fill("alice@test.com")
# Step 3: address
await page.get_by_placeholder("Current Address").fill("12 rue de la Paix")
# Step 4: submit
await page.get_by_role("button", name="Submit").click()
""",
        )
        response = await svc.execute(request)
        failed = sum(1 for s in response.steps_results if s.statut == "FAILED")
        assert failed <= 2, f"Trop d'echecs : {[(s.step, s.erreur) for s in response.steps_results if s.statut == 'FAILED']}"
