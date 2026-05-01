using Application.Setting;
using Domain.Entities.Execution;
using Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Persistance.Data;
using System.Security.Claims;

using Asp.Versioning;
namespace API.Controllers
{
    [Route("api/test-runs")]
    [ApiVersion("1.0")]
    [ApiController]
    [Authorize]
    public class TestRunsController : ControllerBase
    {
        private readonly TestAutoumatisationContext _dbContext;
        private readonly ILogger<TestRunsController> _logger;

        public TestRunsController(TestAutoumatisationContext dbContext, ILogger<TestRunsController> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
        }

        private Guid CurrentUserId => Guid.TryParse(
            User.FindFirst(ClaimTypes.NameIdentifier)?.Value,
            out var userId)
            ? userId
            : Guid.Empty;

        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult> GetAll(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] string? search = null,
            [FromQuery] string? status = null)
        {
            try
            {
                page = Math.Max(1, page);
                pageSize = Math.Clamp(pageSize, 1, 200);

                var roleName = await GetCurrentRoleNameAsync();
                var memberProjectIds = await GetCurrentMemberProjectIdsAsync();

                var query = _dbContext.TestExecutions
                    .AsNoTracking()
                    .Where(e => !e.IsDeleted)
                    .Include(e => e.ExecutedBy)
                    .Include(e => e.Environment)
                    .Include(e => e.Scenario!)
                        .ThenInclude(s => s.Feature)
                        .ThenInclude(f => f.Module)
                        .ThenInclude(m => m.Project)
                    .Include(e => e.TestSuite!)
                        .ThenInclude(ts => ts.Project)
                    .Include(e => e.TestResults)
                    .AsQueryable();

                if (!string.Equals(roleName, "admin", StringComparison.OrdinalIgnoreCase))
                {
                    query = query.Where(e =>
                        e.ExecutedById == CurrentUserId ||
                        (e.Scenario != null && memberProjectIds.Contains(e.Scenario.Feature.Module.ProjectId)) ||
                        (e.TestSuite != null && memberProjectIds.Contains(e.TestSuite.ProjectId)));
                }

                if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<ExecutionStatus>(status, true, out var parsedStatus))
                {
                    query = query.Where(e => e.Status == parsedStatus);
                }

                if (!string.IsNullOrWhiteSpace(search))
                {
                    var normalized = search.Trim().ToLower();
                    query = query.Where(e =>
                        (e.Scenario != null && e.Scenario.Title.ToLower().Contains(normalized)) ||
                        (e.TestSuite != null && e.TestSuite.Name.ToLower().Contains(normalized)) ||
                        (e.Environment != null && e.Environment.Name.ToLower().Contains(normalized)) ||
                        ((e.ExecutedBy.FirstName + " " + e.ExecutedBy.LastName).ToLower().Contains(normalized)) ||
                        e.Id.ToString().ToLower().Contains(normalized));
                }

                var total = await query.CountAsync();

                var executions = await query
                    .OrderByDescending(e => e.StartedAt)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                var items = executions.Select(e =>
                {
                    var totalTests = e.TestResults.Count;
                    var passedTests = e.TestResults.Count(r => r.Status == TestStatus.Passed);
                    var failedTests = e.TestResults.Count(r => r.Status == TestStatus.Failed || r.Status == TestStatus.Error);
                    var skippedTests = e.TestResults.Count(r => r.Status == TestStatus.Skipped);
                    var passRate = totalTests == 0 ? 0 : Math.Round((passedTests * 100.0) / totalTests, 1);
                    var duration = ResolveDuration(e);

                    return new
                    {
                        id = e.Id,
                        runLabel = BuildRunLabel(e),
                        status = e.Status.ToString(),
                        startedAt = e.StartedAt,
                        completedAt = e.CompletedAt,
                        durationSeconds = duration.HasValue ? Math.Round(duration.Value.TotalSeconds, 1) : (double?)null,
                        executedBy = BuildUserDisplayName(e.ExecutedBy?.FirstName, e.ExecutedBy?.LastName),
                        environment = e.Environment?.Name ?? "Unknown",
                        projectId = ResolveProjectId(e),
                        projectName = ResolveProjectName(e),
                        scenarioName = e.Scenario?.Title,
                        moduleName = e.Scenario?.Feature?.Module?.Name,
                        featureName = e.Scenario?.Feature?.Name,
                        totalTests,
                        passedTests,
                        failedTests,
                        skippedTests,
                        passRate
                    };
                }).ToList();

                return Ok(new ResponseHttp
                {
                    Resultat = new
                    {
                        items,
                        total,
                        page,
                        pageSize
                    },
                    Status = StatusCodes.Status200OK
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error processing request.");
                return StatusCode(StatusCodes.Status500InternalServerError, new ResponseHttp
                {
                    FailMessages = "An unexpected error occurred.",
                    Status = StatusCodes.Status500InternalServerError
                });
            }
        }

        [HttpGet("{id:guid}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult> GetById(Guid id)
        {
            try
            {
                var execution = await _dbContext.TestExecutions
                    .AsNoTracking()
                    .Where(e => !e.IsDeleted && e.Id == id)
                    .Include(e => e.ExecutedBy)
                    .Include(e => e.Environment)
                    .Include(e => e.Scenario!)
                        .ThenInclude(s => s.Feature)
                        .ThenInclude(f => f.Module)
                        .ThenInclude(m => m.Project)
                    .Include(e => e.TestSuite!)
                        .ThenInclude(ts => ts.Project)
                    .Include(e => e.Logs)
                    .Include(e => e.TestResults)
                        .ThenInclude(r => r.Scenario)
                    .Include(e => e.TestResults)
                        .ThenInclude(r => r.StepResults)
                        .ThenInclude(sr => sr.Step)
                    .Include(e => e.TestResults)
                        .ThenInclude(r => r.StepResults)
                        .ThenInclude(sr => sr.Screenshot)
                    .FirstOrDefaultAsync();

                if (execution == null)
                {
                    return NotFound(new ResponseHttp
                    {
                        FailMessages = "Test run not found.",
                        Status = StatusCodes.Status404NotFound
                    });
                }

                var roleName = await GetCurrentRoleNameAsync();
                var memberProjectIds = await GetCurrentMemberProjectIdsAsync();
                var executionProjectId = ResolveProjectId(execution);

                var canAccess = string.Equals(roleName, "admin", StringComparison.OrdinalIgnoreCase)
                                || execution.ExecutedById == CurrentUserId
                                || (executionProjectId.HasValue && memberProjectIds.Contains(executionProjectId.Value));

                if (!canAccess)
                {
                    return StatusCode(StatusCodes.Status403Forbidden, new ResponseHttp
                    {
                        FailMessages = "Access denied for this test run.",
                        Status = StatusCodes.Status403Forbidden
                    });
                }

                var totalTests = execution.TestResults.Count;
                var passedTests = execution.TestResults.Count(r => r.Status == TestStatus.Passed);
                var failedTests = execution.TestResults.Count(r => r.Status == TestStatus.Failed || r.Status == TestStatus.Error);
                var skippedTests = execution.TestResults.Count(r => r.Status == TestStatus.Skipped);
                var passRate = totalTests == 0 ? 0 : Math.Round((passedTests * 100.0) / totalTests, 1);
                var duration = ResolveDuration(execution);

                // Load IA HTML reports separately (one per scenario for suite runs).
                // Stored as absolute URLs pointing at the agent's static /reports mount.
                var reports = await _dbContext.Reports
                    .AsNoTracking()
                    .Where(r => r.ExecutionId == execution.Id)
                    .OrderBy(r => r.GeneratedAt)
                    .Select(r => new { id = r.Id, format = r.Format.ToString(), url = r.FilePath })
                    .ToListAsync();
                var primaryReportUrl = reports.FirstOrDefault()?.url;

                var result = new
                {
                    id = execution.Id,
                    runLabel = BuildRunLabel(execution),
                    status = execution.Status.ToString(),
                    startedAt = execution.StartedAt,
                    completedAt = execution.CompletedAt,
                    durationSeconds = duration.HasValue ? Math.Round(duration.Value.TotalSeconds, 1) : (double?)null,
                    browserType = execution.BrowserType,
                    isHeadless = execution.IsHeadless,
                    executedBy = BuildUserDisplayName(execution.ExecutedBy?.FirstName, execution.ExecutedBy?.LastName),
                    environment = execution.Environment?.Name ?? "Unknown",
                    projectId = executionProjectId,
                    projectName = ResolveProjectName(execution),
                    scenarioName = execution.Scenario?.Title,
                    testSuiteName = execution.TestSuite?.Name,
                    reportUrl = primaryReportUrl,
                    reports,
                    totalTests,
                    passedTests,
                    failedTests,
                    skippedTests,
                    passRate,
                    testResults = execution.TestResults
                        .OrderBy(r => r.Scenario?.Title)
                        .Select(r => new
                        {
                            id = r.Id,
                            scenarioId = r.ScenarioId,
                            scenarioName = r.Scenario?.Title ?? "Unknown scenario",
                            status = r.Status.ToString(),
                            errorMessage = r.ErrorMessage,
                            startedAt = r.StartedAt,
                            completedAt = r.CompletedAt,
                            durationSeconds = Math.Round(r.Duration.TotalSeconds, 2),
                            stepResults = r.StepResults
                                .OrderBy(sr => sr.Step.DisplayOrder)
                                .Select(sr => new
                                {
                                    id = sr.Id,
                                    stepText = sr.Step.Text,
                                    stepType = sr.Step.StepType.ToString(),
                                    status = sr.Status.ToString(),
                                    errorMessage = sr.ErrorMessage,
                                    durationSeconds = Math.Round(sr.Duration.TotalSeconds, 2),
                                    actionPerformed = sr.ActionPerformed,
                                    selectorUsed = sr.SelectorUsed,
                                    screenshot = sr.Screenshot == null
                                        ? null
                                        : new
                                        {
                                            id = sr.Screenshot.Id,
                                            fileName = sr.Screenshot.FileName,
                                            filePath = sr.Screenshot.FilePath,
                                            capturedAt = sr.Screenshot.CapturedAt
                                        }
                                })
                                .ToList()
                        })
                        .ToList(),
                    logs = execution.Logs
                        .OrderByDescending(l => l.Timestamp)
                        .Select(l => new
                        {
                            id = l.Id,
                            level = l.Level.ToString(),
                            message = l.Message,
                            details = l.Details,
                            timestamp = l.Timestamp
                        })
                        .ToList()
                };

                return Ok(new ResponseHttp
                {
                    Resultat = result,
                    Status = StatusCodes.Status200OK
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error processing request.");
                return StatusCode(StatusCodes.Status500InternalServerError, new ResponseHttp
                {
                    FailMessages = "An unexpected error occurred.",
                    Status = StatusCodes.Status500InternalServerError
                });
            }
        }

        private static TimeSpan? ResolveDuration(TestExecution execution)
        {
            if (execution.Duration.HasValue)
            {
                return execution.Duration.Value;
            }

            if (execution.CompletedAt.HasValue)
            {
                return execution.CompletedAt.Value - execution.StartedAt;
            }

            return null;
        }

        private static string BuildRunLabel(TestExecution execution)
        {
            if (execution.TestSuite != null)
            {
                return $"Suite: {execution.TestSuite.Name}";
            }

            if (execution.Scenario != null)
            {
                return $"Scenario: {execution.Scenario.Title}";
            }

            return $"Execution {execution.Id.ToString()[..8]}";
        }

        private static string BuildUserDisplayName(string? firstName, string? lastName)
        {
            var full = $"{firstName} {lastName}".Trim();
            return string.IsNullOrWhiteSpace(full) ? "Unknown user" : full;
        }

        private static Guid? ResolveProjectId(TestExecution execution)
        {
            if (execution.Scenario != null)
            {
                return execution.Scenario.Feature.Module.ProjectId;
            }

            if (execution.TestSuite != null)
            {
                return execution.TestSuite.ProjectId;
            }

            return execution.Environment?.ProjectId;
        }

        private static string ResolveProjectName(TestExecution execution)
        {
            if (execution.Scenario?.Feature?.Module?.Project != null)
            {
                return execution.Scenario.Feature.Module.Project.Name;
            }

            if (execution.TestSuite?.Project != null)
            {
                return execution.TestSuite.Project.Name;
            }

            return "Unknown project";
        }

        private async Task<string> GetCurrentRoleNameAsync()
        {
            if (CurrentUserId == Guid.Empty)
            {
                return string.Empty;
            }

            var roleName = await _dbContext.Users
                .AsNoTracking()
                .Where(u => u.Id == CurrentUserId)
                .SelectMany(u => u.UserRoles)
                .Select(ur => ur.Role.Name)
                .FirstOrDefaultAsync();

            return (roleName ?? string.Empty).Trim().ToLowerInvariant();
        }

        private async Task<List<Guid>> GetCurrentMemberProjectIdsAsync()
        {
            if (CurrentUserId == Guid.Empty)
            {
                return new List<Guid>();
            }

            return await _dbContext.ProjectMembers
                .AsNoTracking()
                .Where(m => !m.IsDeleted && m.UserId == CurrentUserId)
                .Select(m => m.ProjectId)
                .Distinct()
                .ToListAsync();
        }
    }
}
