using Application.Interfaces;
using Application.Setting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

using Asp.Versioning;
namespace API.Controllers
{
    /// <summary>
    /// Triggers AI-powered test execution via the IA Test Agent microservice.
    /// </summary>
    [Authorize]
    [Route("api/ai-runs")]
    [ApiVersion("1.0")]
    [ApiController]
    public class AiRunsController : ControllerBase
    {
        private readonly IIAAgentService _iaAgentService;
        private readonly ILogger<AiRunsController> _logger;

        public AiRunsController(IIAAgentService iaAgentService, ILogger<AiRunsController> logger)
        {
            _iaAgentService = iaAgentService;
            _logger = logger;
        }

        private Guid CurrentUserId => Guid.Parse(
            User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? throw new UnauthorizedAccessException("User not authenticated."));

        /// <summary>
        /// Run a scenario through the AI Test Agent pipeline.
        /// The agent parses the Gherkin steps, generates a Playwright script, executes it
        /// and the results are persisted as a standard TestExecution record.
        /// </summary>
        /// <param name="request">ScenarioId + optional target URL override.</param>
        /// <returns>The new TestExecution Id so the client can navigate to the run detail page.</returns>
        [HttpPost]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult> RunScenario(
            [FromBody] AiRunRequest request,
            CancellationToken ct)
        {
            try
            {
                var executionId = await _iaAgentService.RunScenarioAsync(
                    request.ScenarioId,
                    CurrentUserId,
                    request.IsHeadless,
                    ct);

                return Ok(new ResponseHttp
                {
                    Resultat = new { executionId },
                    Status = StatusCodes.Status200OK
                });
            }
            catch (UnauthorizedAccessException)
            {
                return Unauthorized(new ResponseHttp
                {
                    FailMessages = "You must be authenticated to run AI tests.",
                    Status = StatusCodes.Status401Unauthorized
                });
            }
            catch (ArgumentException ex)
            {
                _logger.LogError(ex, "Unexpected error processing request.");
                return NotFound(new ResponseHttp
                {
                    FailMessages = "An unexpected error occurred.",
                    Status = StatusCodes.Status404NotFound
                });
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex, "Unexpected error processing request.");
                return BadRequest(new ResponseHttp
                {
                    FailMessages = "An unexpected error occurred.",
                    Status = StatusCodes.Status400BadRequest
                });
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Unexpected error processing request.");
                return StatusCode(StatusCodes.Status502BadGateway, new ResponseHttp
                {
                    FailMessages = "An unexpected error occurred.",
                    Status = StatusCodes.Status502BadGateway
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

        /// <summary>
        /// Run an entire test suite (multiple scenarios) through the AI Test Agent.
        /// All scenario results are grouped under a single TestExecution record
        /// (TestSuiteId set, ScenarioId null) so they appear as one row in Test Runs.
        /// </summary>
        [HttpPost("test-suite")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult> RunTestSuite(
            [FromBody] AiRunSuiteRequest request,
            CancellationToken ct)
        {
            try
            {
                var executionId = await _iaAgentService.RunTestSuiteAsync(
                    request.TestSuiteId,
                    CurrentUserId,
                    request.IsHeadless,
                    ct);

                return Ok(new ResponseHttp
                {
                    Resultat = new { executionId },
                    Status = StatusCodes.Status200OK
                });
            }
            catch (UnauthorizedAccessException)
            {
                return Unauthorized(new ResponseHttp
                {
                    FailMessages = "You must be authenticated to run AI tests.",
                    Status = StatusCodes.Status401Unauthorized
                });
            }
            catch (ArgumentException ex)
            {
                _logger.LogError(ex, "Unexpected error processing request.");
                return NotFound(new ResponseHttp
                {
                    FailMessages = "An unexpected error occurred.",
                    Status = StatusCodes.Status404NotFound
                });
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex, "Unexpected error processing request.");
                return BadRequest(new ResponseHttp
                {
                    FailMessages = "An unexpected error occurred.",
                    Status = StatusCodes.Status400BadRequest
                });
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Unexpected error processing request.");
                return StatusCode(StatusCodes.Status502BadGateway, new ResponseHttp
                {
                    FailMessages = "An unexpected error occurred.",
                    Status = StatusCodes.Status502BadGateway
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
    }

    /// <summary>Request body for POST /api/ai-runs.</summary>
    public sealed class AiRunRequest
    {
        [Required]
        public Guid ScenarioId { get; set; }

        /// <summary>
        /// When true (default), runs the browser headless. When false, the user
        /// can watch the test executing in a real browser window.
        /// </summary>
        public bool IsHeadless { get; set; } = true;
    }

    /// <summary>Request body for POST /api/ai-runs/test-suite.</summary>
    public sealed class AiRunSuiteRequest
    {
        [Required]
        public Guid TestSuiteId { get; set; }

        /// <summary>When true (default), runs headless; when false, browser is visible.</summary>
        public bool IsHeadless { get; set; } = true;
    }
}
