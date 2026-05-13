using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Collections.Concurrent;
using Application.Interfaces;
using Domain.Entities.Execution;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Persistance.Data;

namespace Persistance.Services
{
    /// <summary>
    /// Calls the Python IA Test Agent microservice and persists the results
    /// as standard TestExecution / TestResult / StepResult entities.
    /// </summary>
    public class IAAgentService : IIAAgentService
    {
        private static readonly ConcurrentDictionary<Guid, CancellationTokenSource> ActiveExecutionTokens = new();

        private readonly HttpClient _http;
        private readonly TestAutoumatisationContext _db;
        private readonly IActionMappingRepository _actionMappings;
        private readonly string _agentBaseUrl;

        public IAAgentService(
            HttpClient http,
            TestAutoumatisationContext db,
            IActionMappingRepository actionMappings,
            IConfiguration config)
        {
            _http = http;
            _db = db;
            _actionMappings = actionMappings;
            _agentBaseUrl = (config["IAAgent:BaseUrl"] ?? "http://localhost:8000").TrimEnd('/');
        }

        public async Task<Guid> RunScenarioAsync(
            Guid scenarioId,
            Guid executedById,
            bool isHeadless = true,
            CancellationToken ct = default)
        {
            var scenario = await LoadScenarioAsync(scenarioId, ct);
            var environment = ResolveEnvironment(scenario.Feature.Module.Project);

            var startedAt = DateTime.UtcNow;
            var execution = new TestExecution
            {
                Id = Guid.NewGuid(),
                ScenarioId = scenarioId,
                TestSuiteId = null,
                EnvironmentId = environment.Id,
                ExecutedById = executedById,
                Status = ExecutionStatus.Running,
                StartedAt = startedAt,
                BrowserType = "Chromium",
                IsHeadless = isHeadless
            };
            _db.TestExecutions.Add(execution);
            var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            ActiveExecutionTokens[execution.Id] = linkedCts;
            // Persist the Running record immediately so the frontend can display
            // the live row in the test-runs list while the agent is executing.
            await _db.SaveChangesAsync(ct);

            try
            {
                var (passed, durationMs) = await RunSingleScenarioAsync(scenario, execution, executedById, isHeadless, linkedCts.Token);

                var completedAt = DateTime.UtcNow;
                execution.CompletedAt = completedAt;
                execution.Duration = TimeSpan.FromMilliseconds(durationMs);
                execution.Status = passed ? ExecutionStatus.Completed : ExecutionStatus.Failed;

                await _db.SaveChangesAsync(CancellationToken.None);
                return execution.Id;
            }
            catch (OperationCanceledException)
            {
                var completedAt = DateTime.UtcNow;
                execution.CompletedAt = completedAt;
                execution.Duration = completedAt - startedAt;
                execution.Status = ExecutionStatus.Cancelled;
                await _db.SaveChangesAsync(CancellationToken.None);
                throw new InvalidOperationException("AI run was cancelled.");
            }
            finally
            {
                ActiveExecutionTokens.TryRemove(execution.Id, out _);
                linkedCts.Dispose();
            }
        }

        public async Task<Guid> RunTestSuiteAsync(
            Guid testSuiteId,
            Guid executedById,
            bool isHeadless = true,
            CancellationToken ct = default)
        {
            // ── 1. Load suite + scenarios (with steps + project + env) ──────────────
            var suite = await _db.TestSuites
                .Include(ts => ts.Project)
                    .ThenInclude(p => p.Environments)
                .Include(ts => ts.TestSuiteScenarios)
                    .ThenInclude(tss => tss.Scenario)
                        .ThenInclude(s => s.Steps)
                .Include(ts => ts.TestSuiteScenarios)
                    .ThenInclude(tss => tss.Scenario)
                        .ThenInclude(s => s.Feature)
                            .ThenInclude(f => f.Module)
                .FirstOrDefaultAsync(ts => ts.Id == testSuiteId && !ts.IsDeleted, ct)
                ?? throw new ArgumentException($"TestSuite {testSuiteId} not found.");

            var orderedScenarios = suite.TestSuiteScenarios
                .Where(tss => tss.Scenario != null && !tss.Scenario.IsDeleted)
                .OrderBy(tss => tss.DisplayOrder)
                .Select(tss => tss.Scenario)
                .ToList();

            if (orderedScenarios.Count == 0)
                throw new InvalidOperationException($"TestSuite '{suite.Name}' has no scenarios to run.");

            var environment = ResolveEnvironment(suite.Project);

            // ── 2. Create ONE TestExecution for the whole suite ─────────────────────
            var startedAt = DateTime.UtcNow;
            var execution = new TestExecution
            {
                Id = Guid.NewGuid(),
                ScenarioId = null,
                TestSuiteId = testSuiteId,
                EnvironmentId = environment.Id,
                ExecutedById = executedById,
                Status = ExecutionStatus.Running,
                StartedAt = startedAt,
                BrowserType = "Chromium",
                IsHeadless = isHeadless
            };
            _db.TestExecutions.Add(execution);
            var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            ActiveExecutionTokens[execution.Id] = linkedCts;
            // Persist the Running record immediately so the frontend can display
            // the live row in the test-runs list while the agent is executing.
            await _db.SaveChangesAsync(ct);

            try
            {
                // ── 3. Run each scenario, accumulating results in the same execution ───
                bool allPassed = true;
                double totalDurationMs = 0;

                foreach (var scenario in orderedScenarios)
                {
                    bool passed;
                    double durationMs;
                    try
                    {
                        (passed, durationMs) = await RunSingleScenarioAsync(scenario, execution, executedById, isHeadless, linkedCts.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        passed = false;
                        durationMs = 0;
                        _db.ExecutionLogs.Add(new ExecutionLog
                        {
                            Id = Guid.NewGuid(),
                            ExecutionId = execution.Id,
                            Level = Domain.Enums.LogLevel.Error,
                            Message = $"AI Agent failed to run scenario '{scenario.Title}'.",
                            Details = ex.Message,
                            Timestamp = DateTime.UtcNow
                        });
                    }

                    allPassed &= passed;
                    totalDurationMs += durationMs;
                }

                execution.CompletedAt = DateTime.UtcNow;
                execution.Duration = TimeSpan.FromMilliseconds(totalDurationMs);
                execution.Status = allPassed ? ExecutionStatus.Completed : ExecutionStatus.Failed;

                await _db.SaveChangesAsync(CancellationToken.None);
                return execution.Id;
            }
            catch (OperationCanceledException)
            {
                var completedAt = DateTime.UtcNow;
                execution.CompletedAt = completedAt;
                execution.Duration = completedAt - startedAt;
                execution.Status = ExecutionStatus.Cancelled;
                await _db.SaveChangesAsync(CancellationToken.None);
                throw new InvalidOperationException("AI run was cancelled.");
            }
            finally
            {
                ActiveExecutionTokens.TryRemove(execution.Id, out _);
                linkedCts.Dispose();
            }
        }

        public async Task<bool> CancelExecutionAsync(Guid executionId)
        {
            // Cooperative cancel — if the run is still active in this process,
            // signal its cancellation token so the worker stops cleanly.
            if (ActiveExecutionTokens.TryGetValue(executionId, out var cts))
            {
                if (!cts.IsCancellationRequested)
                {
                    cts.Cancel();
                }
            }

            // Best-effort DB fallback: even if the token is no longer tracked
            // (e.g. server restart, run started by another instance, or run
            // already past the cancellation point), as long as the execution
            // is still flagged as Running we mark it Cancelled so the user
            // is never stuck with a "running forever" row.
            var execution = await _db.TestExecutions.FirstOrDefaultAsync(e => e.Id == executionId && !e.IsDeleted);
            if (execution == null)
            {
                return false;
            }

            if (execution.Status == ExecutionStatus.Running || execution.Status == ExecutionStatus.Pending)
            {
                execution.Status = ExecutionStatus.Cancelled;
                execution.CompletedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync(CancellationToken.None);
                return true;
            }

            // Already finished (Completed / Failed / Cancelled) — nothing to do
            // but report success so the UI stops showing it as cancellable.
            return execution.Status == ExecutionStatus.Cancelled;
        }

        // ── Private helpers ────────────────────────────────────────────────────────

        private async Task<Domain.Entities.Scenarios.Scenario> LoadScenarioAsync(Guid scenarioId, CancellationToken ct)
        {
            return await _db.Scenarios
                .Include(s => s.Steps)
                .Include(s => s.Feature)
                    .ThenInclude(f => f.Module)
                    .ThenInclude(m => m.Project)
                    .ThenInclude(p => p.Environments)
                .FirstOrDefaultAsync(s => s.Id == scenarioId && !s.IsDeleted, ct)
                ?? throw new ArgumentException($"Scenario {scenarioId} not found.");
        }

        private Domain.Entities.TestData.Environment ResolveEnvironment(Domain.Entities.ProjectManagement.Project project)
        {
            var environment =
                project.Environments.FirstOrDefault(e => e.IsDefault)
                ?? project.Environments.FirstOrDefault();

            // Auto-create a lightweight "AI Default" environment when the project has none.
            // The actual URL comes from the Gherkin steps, so BaseUrl is not required here.
            if (environment == null)
            {
                environment = new Domain.Entities.TestData.Environment
                {
                    Id = Guid.NewGuid(),
                    ProjectId = project.Id,
                    Name = "AI Default",
                    BaseUrl = "",
                    IsDefault = true
                };
                _db.Environments.Add(environment);
            }
            return environment;
        }

        /// <summary>
        /// Runs ONE scenario through the IA Agent and adds the resulting
        /// TestResult / StepResults / Screenshots / Report / ExecutionLog
        /// entries to the EF Core context (no SaveChanges).
        /// Returns (passed, durationMs).
        /// </summary>
        private async Task<(bool passed, double durationMs)> RunSingleScenarioAsync(
            Domain.Entities.Scenarios.Scenario scenario,
            TestExecution execution,
            Guid executedById,
            bool isHeadless,
            CancellationToken ct)
        {
            // Only include non-deleted steps — soft-deleted steps (from prior edits)
            // must be excluded or they inflate the step count sent to the agent.
            var orderedSteps = scenario.Steps
                .Where(s => !s.IsDeleted)
                .OrderBy(s => s.DisplayOrder)
                .ToList();

            // ── Load project-level Action Mappings so the agent can override
            // its NLP-derived selectors when an admin has hand-tuned them. ──
            var projectId = scenario.Feature.Module.ProjectId;
            var mappings = await _actionMappings.GetByProjectIdAsync(projectId, ct);
            var actionMappingPayload = mappings.Select(m => new
            {
                intent_pattern = m.IntentPattern,
                action_type = m.ActionType.ToString().ToLowerInvariant(),
                selector_strategy = (m.SelectorStrategy ?? "css").ToLowerInvariant(),
                selector_value = m.SelectorValue ?? string.Empty,
                description = m.Description ?? string.Empty
            }).ToList();

            // url_cible is intentionally empty: the target URL is already embedded
            // in the Gherkin steps (e.g. "Given I navigate to https://...").
            var pipelineRequest = new
            {
                scenario_name = scenario.Title,
                steps = orderedSteps.Select(s => new
                {
                    keyword = s.StepType.ToString(),
                    text = s.Text
                }),
                url_cible = "",
                language = "fr",
                headless = isHeadless,
                action_mappings = actionMappingPayload
            };

            var httpResponse = await _http.PostAsJsonAsync(
                $"{_agentBaseUrl}/api/ia/pipeline",
                pipelineRequest,
                ct);

            httpResponse.EnsureSuccessStatusCode();

            var snakeOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                PropertyNameCaseInsensitive = true
            };

            var agent = await httpResponse.Content.ReadFromJsonAsync<AgentPipelineResponse>(
                snakeOptions,
                cancellationToken: ct)
                ?? throw new InvalidOperationException("Empty response from IA Agent.");

            // ── Map to domain entities ──────────────────────────────────────────────
            var now = DateTime.UtcNow;
            var durationMs = agent.DurationMs;
            // The IA Agent returns top-level statut = "PASSED" or "FAILED".
            var agentPassed = string.Equals(agent.Statut, "PASSED", StringComparison.OrdinalIgnoreCase)
                              || string.Equals(agent.Statut, "OK", StringComparison.OrdinalIgnoreCase);

            var testResult = new TestResult
            {
                Id = Guid.NewGuid(),
                ExecutionId = execution.Id,
                ScenarioId = scenario.Id,
                Status = agentPassed ? TestStatus.Passed : TestStatus.Failed,
                ErrorMessage = agent.GherkinStepsResults
                    .Select(g => g.Erreur)
                    .FirstOrDefault(e => !string.IsNullOrWhiteSpace(e)),
                Duration = TimeSpan.FromMilliseconds(durationMs),
                StartedAt = now.AddMilliseconds(-durationMs),
                CompletedAt = now,
                AiAnalysisJson = agent.ScenarioAnalysis != null && agent.ScenarioAnalysis.Count > 0
                    ? JsonSerializer.Serialize(agent.ScenarioAnalysis)
                    : null
            };
            _db.TestResults.Add(testResult);

            // ── Map step results ───────────────────────────────────────────────────
            // The agent now returns gherkin_steps_results: exactly one entry per
            // Gherkin step with status/duration/error already aggregated.
            // This avoids having to group raw executor sub-steps by step number.
            var gherkinResults = agent.GherkinStepsResults;
            var stepResultsToAdd = new List<StepResult>();

            for (int i = 0; i < gherkinResults.Count && i < orderedSteps.Count; i++)
            {
                var gr = gherkinResults[i];
                var dbStep = orderedSteps[i];

                var sr = new StepResult
                {
                    Id = Guid.NewGuid(),
                    TestResultId = testResult.Id,
                    StepId = dbStep.Id,
                    Status = gr.Statut == "OK" ? StepStatus.Passed : StepStatus.Failed,
                    ErrorMessage = string.IsNullOrWhiteSpace(gr.Erreur) ? null : gr.Erreur,
                    Duration = TimeSpan.FromMilliseconds(gr.DureeMs),
                    ActionPerformed = dbStep.Text,
                    SelectorUsed = gr.Selector ?? string.Empty,
                    AiAnalysisJson = gr.AiAnalysis != null && gr.AiAnalysis.Count > 0
                        ? JsonSerializer.Serialize(gr.AiAnalysis)
                        : null
                };
                stepResultsToAdd.Add(sr);

                // Screenshot: use the one taken after the last raw sub-step of this
                // Gherkin step.  The agent screenshots list is ordered the same as
                // the raw steps_results, so we map by Gherkin step index (0-based).
                // If the screenshots list is shorter, skip gracefully.
                if (i < agent.Screenshots.Count)
                {
                    var path = agent.Screenshots[i];
                    if (!string.IsNullOrWhiteSpace(path))
                    {
                        // Convert the local file-system path returned by the IA agent
                        // (e.g. "reports/screenshots/xxx.png") to a root-relative URL
                        // served via nginx → ia-agent static /reports mount.
                        // Using a relative path avoids embedding the internal Docker
                        // hostname (ia-agent:8000) which is not resolvable by the browser.
                        var screenshotRelative = path.Replace('\\', '/').TrimStart('/');
                        var screenshotUrl = screenshotRelative.StartsWith("reports/", StringComparison.OrdinalIgnoreCase)
                            ? $"/{screenshotRelative}"
                            : $"/reports/{screenshotRelative}";

                        _db.Screenshots.Add(new Screenshot
                        {
                            Id = Guid.NewGuid(),
                            StepResultId = sr.Id,
                            FilePath = screenshotUrl,
                            FileName = Path.GetFileName(path),
                            CapturedAt = now,
                            FileSize = 0
                        });
                    }
                }
            }
            _db.StepResults.AddRange(stepResultsToAdd);

            // ── Persist the IA HTML report so the UI can embed/open it later ──────
            // The agent returns a relative path like "reports/html/report_xxx.html".
            // We store the absolute URL served by the agent's static mount.
            if (!string.IsNullOrWhiteSpace(agent.ReportHtmlPath))
            {
                var reportRelative = agent.ReportHtmlPath
                    .Replace('\\', '/')
                    .TrimStart('/');
                // Strip a leading "reports/" segment if present, since the static mount
                // is exposed at "/reports".
                var reportUrl = reportRelative.StartsWith("reports/", StringComparison.OrdinalIgnoreCase)
                    ? $"/{reportRelative}"
                    : $"/reports/{reportRelative}";

                _db.Reports.Add(new Domain.Entities.Reporting.Report
                {
                    Id = Guid.NewGuid(),
                    ExecutionId = execution.Id,
                    Format = Domain.Enums.ReportFormat.HTML,
                    FilePath = reportUrl,
                    GeneratedAt = now,
                    GeneratedById = executedById
                });
            }

            // ── Persist the IA Technical Trace (vision-aware diagnostic page) ──────
            // A second Report row alongside the AI execution report; the frontend
            // disambiguates by URL pattern (contains "technical_trace_").
            if (!string.IsNullOrWhiteSpace(agent.TechnicalTracePath))
            {
                var ttRel = agent.TechnicalTracePath.Replace('\\', '/').TrimStart('/');
                var ttUrl = ttRel.StartsWith("reports/", StringComparison.OrdinalIgnoreCase)
                    ? $"/{ttRel}"
                    : $"/reports/{ttRel}";
                _db.Reports.Add(new Domain.Entities.Reporting.Report
                {
                    Id = Guid.NewGuid(),
                    ExecutionId = execution.Id,
                    Format = Domain.Enums.ReportFormat.HTML,
                    FilePath = ttUrl,
                    GeneratedAt = now,
                    GeneratedById = executedById
                });
            }

            // Execution log
            _db.ExecutionLogs.Add(new ExecutionLog
            {
                Id = Guid.NewGuid(),
                ExecutionId = execution.Id,
                Level = Domain.Enums.LogLevel.Info,
                Message = $"AI Agent ran scenario '{scenario.Title}' — result: {agent.Statut}",
                Details = $"ia_test_id={agent.TestId} | duration={durationMs:F0}ms | " +
                          $"steps={agent.StepsResults.Count} | " +
                          $"report={agent.ReportHtmlPath}",
                Timestamp = now
            });

            return (agentPassed, durationMs);
        }

        // ── DTOs for IA Agent response ─────────────────────────────────────────────

        private sealed class AgentPipelineResponse
        {
            [JsonPropertyName("test_id")]
            public string TestId { get; set; } = string.Empty;

            [JsonPropertyName("scenario_name")]
            public string ScenarioName { get; set; } = string.Empty;

            [JsonPropertyName("statut")]
            public string Statut { get; set; } = "FAILED";

            [JsonPropertyName("duree_ms")]
            public double DurationMs { get; set; }

            [JsonPropertyName("steps_results")]
            public List<AgentStepResult> StepsResults { get; set; } = new();

            [JsonPropertyName("screenshots")]
            public List<string> Screenshots { get; set; } = new();

            [JsonPropertyName("report_html_path")]
            public string ReportHtmlPath { get; set; } = string.Empty;

            [JsonPropertyName("pipeline_trace_path")]
            public string PipelineTracePath { get; set; } = string.Empty;

            [JsonPropertyName("technical_trace_path")]
            public string TechnicalTracePath { get; set; } = string.Empty;

            /// <summary>
            /// One entry per Gherkin step, aggregated from raw sub-step results.
            /// Use this instead of StepsResults for 1-to-1 mapping with Gherkin steps.
            /// </summary>
            [JsonPropertyName("gherkin_steps_results")]
            public List<AgentGherkinStepResult> GherkinStepsResults { get; set; } = new();

            /// <summary>
            /// Aggregated AI failure analysis for the whole scenario when at least one
            /// step has failed.  Empty/null when the scenario passed.
            /// </summary>
            [JsonPropertyName("scenario_analysis")]
            public Dictionary<string, object>? ScenarioAnalysis { get; set; }
        }

        private sealed class AgentGherkinStepResult
        {
            [JsonPropertyName("step_num")]
            public int StepNum { get; set; }

            [JsonPropertyName("keyword")]
            public string Keyword { get; set; } = string.Empty;

            [JsonPropertyName("gherkin_text")]
            public string GherkinText { get; set; } = string.Empty;

            [JsonPropertyName("statut")]
            public string Statut { get; set; } = "OK";

            [JsonPropertyName("duree_ms")]
            public double DureeMs { get; set; }

            [JsonPropertyName("erreur")]
            public string Erreur { get; set; } = string.Empty;

            [JsonPropertyName("selector")]
            public string Selector { get; set; } = string.Empty;

            /// <summary>
            /// AI-generated failure analysis for this step (only present when the step
            /// failed).  Stored as a free-form dictionary so the frontend can display
            /// any new fields without requiring a backend change.
            /// </summary>
            [JsonPropertyName("ai_analysis")]
            public Dictionary<string, object>? AiAnalysis { get; set; }
        }

        private sealed class AgentStepResult
        {
            [JsonPropertyName("step")]
            public string Step { get; set; } = string.Empty;

            [JsonPropertyName("statut")]
            public string Statut { get; set; } = "FAILED";

            [JsonPropertyName("duree_ms")]
            public double DurationMs { get; set; }

            [JsonPropertyName("erreur")]
            public string Erreur { get; set; } = string.Empty;

            [JsonPropertyName("retry_count")]
            public int RetryCount { get; set; }

            [JsonPropertyName("visual_fallback_used")]
            public bool VisualFallbackUsed { get; set; }

            [JsonPropertyName("adaptation_appliquee")]
            public string AdaptationAppliquee { get; set; } = string.Empty;
        }
    }
}
