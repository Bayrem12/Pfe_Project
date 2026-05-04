"""
Endpoint du pipeline complet.
POST /api/ia/pipeline
Orchestre: parse Gherkin → generate script → execute → report.

Day 14/15: expose recovery_stats dans la reponse du pipeline.
"""

import re
from datetime import datetime

from pydantic import BaseModel, Field
from fastapi import APIRouter, HTTPException

from app.schemas.gherkin_schemas import GherkinStep
from app.schemas.report_schemas import TestReportResponse, PipelineTraceData, StepTraceInfo
from app.schemas.test_schemas import RecoveryStats, StepResult
from app.services.nlp_service import NLPService
from app.services.generator_service import GeneratorService
from app.services.executor_service import ExecutorService
from app.services.report_service import ReportService, _classify_error, _classify_method
from app.services.failure_analysis_service import (
    analyze_step_failure,
    analyze_scenario_failure,
)

router = APIRouter(prefix="/api/ia", tags=["Pipeline"])

nlp_service = NLPService()
generator_service = GeneratorService()
executor_service = ExecutorService()
report_service = ReportService()


class PipelineRequest(BaseModel):
    """Requete du pipeline complet."""
    scenario_name: str = Field(..., description="Nom du scenario")
    steps: list[GherkinStep] = Field(..., description="Liste des steps Gherkin")
    url_cible: str = Field("", description="URL de l'application a tester")
    language: str = Field("fr", description="Langue du scenario (fr/en)")
    headless: bool = Field(
        True,
        description="Si False, le navigateur est visible (mode debug — écouter le test).",
    )
    action_mappings: list[dict] = Field(
        default_factory=list,
        description="Project-level overrides: list of {intent_pattern, action_type, "
        "selector_strategy, selector_value, description}. When a step's raw_text "
        "matches intent_pattern (regex), the generator uses the mapping instead of NLP.",
    )


class PipelineResponse(BaseModel):
    """Reponse du pipeline complet."""
    test_id: str
    scenario_name: str
    statut: str
    duree_ms: float
    # NLP result summary
    nombre_steps_analyses: int
    intentions_detectees: list[str]
    # Generated script
    script_code: str
    # Execution results — raw (one entry per await line in the generated script)
    steps_results: list[dict]
    screenshots: list[str]
    # Robustesse (Day 14/15)
    recovery_stats: RecoveryStats = RecoveryStats()
    # Reports
    report_html_path: str = ""
    pipeline_trace_path: str = ""
    technical_trace_path: str = ""
    # Gherkin-aggregated results — one entry per Gherkin step, aggregated from
    # all raw steps_results that belong to that step.  Consumers should use this
    # field instead of steps_results when they need a 1-to-1 mapping with Gherkin.
    gherkin_steps_results: list[dict] = []
    # AI failure analysis — one verdict for the whole scenario.  Empty dict when
    # the scenario passed.  Each failed entry inside gherkin_steps_results also
    # carries its own ai_analysis sub-dict.
    scenario_analysis: dict = Field(default_factory=dict)


def _extract_selector(code_lines: list[str]) -> str:
    """Extract the primary Playwright selector from generated code lines for a step."""
    for line in code_lines:
        # page.locator("...") — double-quoted
        m = re.search(r'page\.locator\("([^"]+)"', line)
        if m:
            return m.group(1)
        # page.locator('...') — single-quoted
        m = re.search(r"page\.locator\('([^']+)'", line)
        if m:
            return m.group(1)
        # page.get_by_text("..."), page.get_by_role(...), etc. — double-quoted
        m = re.search(r'page\.(get_by_\w+)\("([^"]+)"', line)
        if m:
            return f"{m.group(1)}({m.group(2)})"
        # page.get_by_text('...') — single-quoted
        m = re.search(r"page\.(get_by_\w+)\('([^']+)'", line)
        if m:
            return f"{m.group(1)}({m.group(2)})"
    return ""


def _parse_script_steps(script_code: str) -> dict[int, list[str]]:
    """
    Parse script_code and return {step_num: [code_line, ...]} for each "# ── Step N:" block.
    Uses the same detection logic as _execute_script.
    Replaces _parse_script_step_exec_counts — counts are derived from len(lines).
    """
    code_lines: dict[int, list[str]] = {}
    current_step = 0
    inside_helper = False

    for line in script_code.splitlines():
        stripped = line.strip()

        if re.match(r"^async def _\w+\(|^def _\w+\(", line):
            inside_helper = True
            continue
        if re.match(r"^async def \w+\(|^async def test_", line):
            inside_helper = False
            continue
        if line and not line[0].isspace() and not stripped.startswith("#"):
            inside_helper = False
        if inside_helper:
            continue

        # Step comment detection (same fix as _execute_script)
        if stripped.startswith("#") and re.search(r"[Ss]tep\s+\d+", stripped):
            m = re.search(r"[Ss]tep\s+(\d+)", stripped)
            if m:
                current_step = int(m.group(1))
        elif ("await page." in stripped or "await expect" in stripped) and not stripped.startswith("#"):
            if current_step > 0:
                code_lines.setdefault(current_step, []).append(stripped)

    return code_lines


def _build_pipeline_trace(
    test_id: str,
    scenario_name: str,
    gherkin_steps: list[GherkinStep],
    parsed_steps,       # list[ParsedStep]
    script_code: str,
    exec_results: list[StepResult],
    url_cible: str,
    language: str,
    overall_statut: str,
    total_duree_ms: float,
    recovery_stats_dict: dict,
    screenshots: list[str] | None = None,
) -> PipelineTraceData:
    """Construit la PipelineTraceData à partir des résultats de toutes les phases."""

    # ── Extract actual code lines from the script (one list per step block)
    code_lines_by_step = _parse_script_steps(script_code)

    # ── Exclude the initial "Navigate to {url}" StepResult that execute() prepends
    #    before running the script.  Those results don't correspond to any script step
    #    and would shift every partition index by 1.
    script_exec_results = [
        r for r in exec_results if not r.step.startswith("Navigate to ")
    ]

    # ── Partition script_exec_results by step.
    # Since 2026-04-28, executor groups all `await` lines belonging to the
    # same Gherkin step into a single StepResult, so the mapping is now
    # simply one StepResult per step (in declaration order). The historical
    # "1 entry per await line" partitioning is preserved as a fallback for
    # legacy result shapes (still useful when execute_live emits per-line
    # results from a different code path).
    step_exec_map: dict[int, list[StepResult]] = {}
    if len(script_exec_results) == len(code_lines_by_step):
        # New shape: 1 StepResult per step.
        for i, step_num in enumerate(sorted(code_lines_by_step.keys())):
            step_exec_map[step_num] = [script_exec_results[i]]
    else:
        # Legacy shape: 1 StepResult per script line.
        idx = 0
        for step_num in sorted(code_lines_by_step.keys()):
            count = len(code_lines_by_step[step_num])
            step_exec_map[step_num] = script_exec_results[idx: idx + count]
            idx += count

    trace_steps: list[StepTraceInfo] = []
    for i, gherkin_step in enumerate(gherkin_steps):
        step_num = i + 1
        exec_grp = step_exec_map.get(step_num, [])

        # Fallback: if the mapping missed this step (e.g., script parsing mismatch),
        # use no exec results for this step
        nlp_step = parsed_steps[i] if i < len(parsed_steps) else None

        all_ok = all(r.statut == "OK" for r in exec_grp) if exec_grp else True
        failed_r = next((r for r in exec_grp if r.statut == "FAILED"), None)

        statut = "OK" if all_ok and exec_grp else ("FAILED" if not all_ok else "")
        erreur = failed_r.erreur if failed_r else ""
        duree_ms = sum(r.duree_ms for r in exec_grp)
        visual_fallback_used = any(r.visual_fallback_used for r in exec_grp)
        retry_count = max((r.retry_count for r in exec_grp), default=0)
        adaptation = next((r.adaptation_appliquee for r in exec_grp if r.adaptation_appliquee), "")

        erreur_type, erreur_explication = _classify_error(erreur)
        method_used = _classify_method(statut, visual_fallback_used, retry_count, adaptation)

        entites = []
        if nlp_step:
            entites = [{"nom": e.nom, "valeur": e.valeur, "type": e.type_entite} for e in nlp_step.entites]

        generated_code = code_lines_by_step.get(step_num, [])

        trace_steps.append(StepTraceInfo(
            step_num=step_num,
            keyword=gherkin_step.keyword,
            gherkin_text=gherkin_step.text,
            intention=nlp_step.intention if nlp_step else "",
            entites=entites,
            generated_code=generated_code,
            statut=statut,
            duree_ms=duree_ms,
            erreur=erreur,
            erreur_type=erreur_type,
            erreur_explication=erreur_explication,
            method_used=method_used,
            retry_count=retry_count,
            visual_fallback_used=visual_fallback_used,
            playwright_error=next((r.playwright_error for r in exec_grp if r.playwright_error), ""),
            fallback_trace=next((r.fallback_trace for r in exec_grp if r.fallback_trace), None),
        ))

    return PipelineTraceData(
        test_id=test_id,
        scenario_name=scenario_name,
        statut=overall_statut,
        duree_ms=total_duree_ms,
        url_cible=url_cible,
        language=language,
        date_generated=datetime.now().isoformat(),
        steps=trace_steps,
        recovery_stats=recovery_stats_dict,
        screenshots=screenshots or [],
    )


@router.post(
    "/pipeline",
    response_model=PipelineResponse,
    summary="Executer le pipeline complet",
    description="Parse le scenario Gherkin, genere un script Playwright, "
    "l'execute et produit un rapport.",
)
async def run_pipeline(request: PipelineRequest):
    """Pipeline complet : parse → generate → execute → report."""
    try:
        # ── 1. NLP Parsing ──
        from app.schemas.gherkin_schemas import GherkinScenarioRequest

        parse_request = GherkinScenarioRequest(
            scenario_name=request.scenario_name,
            steps=request.steps,
            language=request.language,
        )
        parse_result = nlp_service.parse_scenario(parse_request)
        intentions = [s.intention for s in parse_result.steps_analyses]

        # ── 2. Script Generation ──
        from app.schemas.test_schemas import TestGenerationRequest

        gen_request = TestGenerationRequest(
            scenario_name=request.scenario_name,
            url_cible=request.url_cible,
            steps=parse_result.steps_analyses,
            action_mappings=request.action_mappings or [],
        )
        gen_result = generator_service.generate(gen_request)

        # ── 3. Execution ──
        from app.schemas.test_schemas import TestExecutionRequest

        exec_request = TestExecutionRequest(
            scenario_name=request.scenario_name,
            script_code=gen_result.script_code,
            url_cible=request.url_cible,
            headless=request.headless,
        )
        exec_result = await executor_service.execute(exec_request)

        # ── 4. Report ──
        report = report_service.build_from_execution(
            exec_result, url_cible=request.url_cible
        )

        # ── 5. Pipeline trace ──
        from app.services.report_service import _compute_recovery_stats
        recovery_dict = _compute_recovery_stats(exec_result.steps_results)

        trace = _build_pipeline_trace(
            test_id=exec_result.test_id,
            scenario_name=request.scenario_name,
            gherkin_steps=request.steps,
            parsed_steps=parse_result.steps_analyses,
            script_code=gen_result.script_code,
            exec_results=exec_result.steps_results,
            url_cible=request.url_cible,
            language=request.language,
            overall_statut=exec_result.statut,
            total_duree_ms=exec_result.duree_ms,
            recovery_stats_dict=recovery_dict,
            screenshots=exec_result.screenshots,
        )
        report_service.store_pipeline_trace(trace)

        # ── 6. Generate HTML files ──
        html_path = report_service.generate_html(report.test_id) or ""
        pipeline_trace_path = report_service.generate_pipeline_html(exec_result.test_id) or ""
        technical_trace_path = report_service.generate_technical_trace_html(exec_result.test_id) or ""

        # ── 7. AI failure analysis ─────────────────────────────────────
        # Build the per-Gherkin-step result list, attaching ai_analysis to any
        # entry whose statut is not "OK".  Then derive a scenario-level verdict.
        gherkin_payload: list[dict] = []
        failed_for_summary: list[dict] = []
        for s in trace.steps:
            statut = s.statut if s.statut else "OK"
            entry: dict = {
                "step_num": s.step_num,
                "keyword": s.keyword,
                "gherkin_text": s.gherkin_text,
                "statut": statut,
                "duree_ms": s.duree_ms,
                "erreur": s.erreur,
                "selector": _extract_selector(s.generated_code),
            }
            if statut.upper() not in ("OK", "PASSED"):
                # Pull retry / fallback info from the matching raw exec entry, if any.
                retry_count = 0
                visual_fallback_used = False
                for raw in exec_result.steps_results:
                    if getattr(raw, "step_num", None) == s.step_num:
                        retry_count = max(retry_count, getattr(raw, "retry_count", 0) or 0)
                        if getattr(raw, "visual_fallback_used", False):
                            visual_fallback_used = True
                analysis = analyze_step_failure(
                    step_text=s.gherkin_text,
                    error_message=s.erreur or "",
                    selector=entry["selector"],
                    keyword=s.keyword,
                    visual_fallback_used=visual_fallback_used,
                    retry_count=retry_count,
                )
                entry["ai_analysis"] = analysis.to_dict()
                failed_for_summary.append(entry)
            gherkin_payload.append(entry)

        scenario_analysis = (
            analyze_scenario_failure(
                scenario_name=request.scenario_name,
                failed_steps=failed_for_summary,
            )
            if failed_for_summary
            else {}
        )

        return PipelineResponse(
            test_id=exec_result.test_id,
            scenario_name=request.scenario_name,
            statut=exec_result.statut,
            duree_ms=exec_result.duree_ms,
            nombre_steps_analyses=parse_result.nombre_steps,
            intentions_detectees=intentions,
            script_code=gen_result.script_code,
            steps_results=[s.model_dump() for s in exec_result.steps_results],
            screenshots=exec_result.screenshots,
            recovery_stats=exec_result.recovery_stats,
            report_html_path=html_path,
            pipeline_trace_path=pipeline_trace_path,
            technical_trace_path=technical_trace_path,
            gherkin_steps_results=gherkin_payload,
            scenario_analysis=scenario_analysis,
        )

    except Exception as e:
        raise HTTPException(
            status_code=500,
            detail=f"Erreur pipeline : {str(e)}",
        )

