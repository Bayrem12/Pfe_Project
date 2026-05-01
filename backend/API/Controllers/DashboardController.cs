using Application.Features.DashboardFeature.Queries;
using Application.Setting;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using Asp.Versioning;
namespace API.Controllers
{
    [Route("api/dashboard")]
    [ApiVersion("1.0")]
    [ApiController]
    [Authorize]
    public class DashboardController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly ILogger<DashboardController> _logger;

        public DashboardController(IMediator mediator, ILogger<DashboardController> logger)
        {
            _mediator = mediator;
            _logger = logger;
        }

        /// <summary>
        /// GET /api/dashboard/summary
        /// Returns global platform statistics: total projects, scenarios, executions, pass rate.
        /// </summary>
        [HttpGet("summary")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult> GetSummary()
        {
            try
            {
                var result = await _mediator.Send(new GetDashboardSummaryQuery());
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error processing request.");
                _logger.LogError(ex, "Error retrieving dashboard summary");
                return StatusCode(StatusCodes.Status500InternalServerError, new ResponseHttp { FailMessages = "An error occurred while processing the request.", Status = StatusCodes.Status500InternalServerError });
            }
        }

        /// <summary>
        /// GET /api/dashboard/projects/{projectId}/statistics
        /// Returns statistics for a specific project.
        /// </summary>
        [HttpGet("projects/{projectId}/statistics")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult> GetProjectStatistics(Guid projectId)
        {
            try
            {
                var result = await _mediator.Send(new GetProjectStatisticsQuery(projectId));

                if (result.Status == StatusCodes.Status400BadRequest)
                    return BadRequest(result);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error processing request.");
                _logger.LogError(ex, "Error retrieving statistics for project {ProjectId}", projectId);
                return StatusCode(StatusCodes.Status500InternalServerError, new ResponseHttp { FailMessages = "An error occurred while processing the request.", Status = StatusCodes.Status500InternalServerError });
            }
        }

        /// <summary>
        /// GET /api/dashboard/projects/{projectId}/trends?days=30
        /// Returns daily execution trend data for charting.
        /// </summary>
        [HttpGet("projects/{projectId}/trends")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult> GetExecutionTrends(Guid projectId, [FromQuery] int days = 30)
        {
            try
            {
                var result = await _mediator.Send(new GetExecutionTrendsQuery(projectId, days));
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error processing request.");
                _logger.LogError(ex, "Error retrieving execution trends for project {ProjectId}", projectId);
                return StatusCode(StatusCodes.Status500InternalServerError, new ResponseHttp { FailMessages = "An error occurred while processing the request.", Status = StatusCodes.Status500InternalServerError });
            }
        }

        /// <summary>
        /// GET /api/dashboard/audit-logs?page=1&amp;pageSize=50
        /// Returns paginated audit logs sorted by most recent first.
        /// </summary>
        [HttpGet("audit-logs")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult> GetAuditLogs([FromQuery] int page = 1, [FromQuery] int pageSize = 50)
        {
            try
            {
                var result = await _mediator.Send(new GetAuditLogsQuery(page, pageSize));
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error processing request.");
                _logger.LogError(ex, "Error retrieving audit logs");
                return StatusCode(StatusCodes.Status500InternalServerError, new ResponseHttp { FailMessages = "An error occurred while processing the request.", Status = StatusCodes.Status500InternalServerError });
            }
        }
    }
}
