// NLP Controller — handles all NLP-related API endpoints.
// Route: /api/Nlp/...
//
// This controller follows the same pattern as TestControllerNew:
// 1. Receive HTTP request
// 2. Validate input using FluentValidation
// 3. Send Command/Query to MediatR
// 4. Return the ResponseHttp result
//
// The controller is THIN — it doesn't contain business logic.
// All logic is in the MediatR Handlers (Application layer).
// This is a key Clean Architecture principle: controllers only route requests.

using Application.Features.NlpFeature.Commands;
using Application.Features.NlpFeature.Queries;
using Application.Features.NlpFeature.Dtos;
using Application.Setting;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Security.Claims;

namespace API.Controllers
{
    [Route("api/nlp")]
    [ApiController]
    [Authorize]
    public class NlpController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly ILogger<NlpController> _logger;

        public NlpController(IMediator mediator, ILogger<NlpController> logger)
        {
            _mediator = mediator;
            _logger = logger;
        }

        private bool IsViewerRole()
        {
            return User.Claims.Any(c =>
                       c.Type == ClaimTypes.Role
                       && string.Equals(c.Value, "Viewer", StringComparison.OrdinalIgnoreCase))
                   || User.IsInRole("Viewer");
        }

        private ActionResult ViewerWriteForbidden(string actionLabel)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new ResponseHttp
            {
                Status = StatusCodes.Status403Forbidden,
                Fail_Messages = $"Viewer role is read-only and cannot {actionLabel} NLP mappings."
            });
        }

        /// <summary>
        /// POST /api/Nlp/analyze/{scenarioId}
        /// Analyzes all steps of a scenario using NLP.
        /// Returns an array of StepAnalysis objects with detected intents.
        /// </summary>
        /// <param name="scenarioId">The GUID of the scenario to analyze</param>
        [HttpPost("analyze/{scenarioId}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult> AnalyzeScenario(Guid scenarioId)
        {
            try
            {
                // Create the MediatR command with the scenario ID from the URL
                var command = new AnalyzeScenarioCommand(scenarioId);

                // Send to MediatR — it finds the AnalyzeScenarioCommandHandler and calls Handle()
                var result = await _mediator.Send(command);

                // Return appropriate HTTP status based on the result
                if (result.Status == StatusCodes.Status400BadRequest)
                    return BadRequest(result);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error analyzing scenario {ScenarioId}", scenarioId);
                return StatusCode(StatusCodes.Status500InternalServerError, new ResponseHttp { Fail_Messages = "An error occurred while processing the request.", Status = StatusCodes.Status500InternalServerError });
            }
        }

        /// <summary>
        /// POST /api/Nlp/parse
        /// Parses raw Gherkin text into structured steps.
        /// Request body: { "gherkinContent": "Given I am on login page\nWhen I click login" }
        /// </summary>
        [HttpPost("parse")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult> ParseGherkin([FromBody] ParseGherkinCommand command)
        {
            try
            {
                // ValidationBehavior in the MediatR pipeline handles input validation
                var parseResult = await _mediator.Send(command);

                if (parseResult.Status == StatusCodes.Status400BadRequest)
                    return BadRequest(parseResult);

                return Ok(parseResult);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing Gherkin content");
                return StatusCode(StatusCodes.Status500InternalServerError, new ResponseHttp { Fail_Messages = "An error occurred while processing the request.", Status = StatusCodes.Status500InternalServerError });
            }
        }

        /// <summary>
        /// POST /api/Nlp/action-mappings/{projectId}
        /// Creates a new action mapping that links an NLP intent pattern to a UI action.
        /// The projectId comes from the URL, the rest comes from the JSON body.
        /// Returns 201 Created with the full ActionMapping object.
        /// </summary>
        /// <param name="projectId">The GUID of the project</param>
        /// <param name="request">The action mapping details from the JSON body</param>
        [HttpPost("action-mappings/{projectId}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult> CreateActionMapping(
            Guid projectId,
            [FromBody] CreateActionMappingRequest request)
        {
            try
            {
                if (IsViewerRole())
                    return ViewerWriteForbidden("create");

                // Build the full command by combining URL parameter + body data
                var command = new CreateActionMappingCommand(
                    projectId,
                    request.IntentPattern,
                    request.ActionType,
                    request.SelectorStrategy,
                    request.SelectorValue,
                    request.Description,
                    request.Priority
                );

                // ValidationBehavior in the MediatR pipeline handles input validation
                var createResult = await _mediator.Send(command);

                // Return 201 Created if successful (matches Swagger spec)
                if (createResult.Status == StatusCodes.Status201Created)
                    return StatusCode(StatusCodes.Status201Created, createResult);

                return BadRequest(createResult);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating action mapping for project {ProjectId}", projectId);
                return StatusCode(StatusCodes.Status500InternalServerError, new ResponseHttp { Fail_Messages = "An error occurred while processing the request.", Status = StatusCodes.Status500InternalServerError });
            }
        }

        /// <summary>
        /// GET /api/Nlp/action-mappings/{projectId}
        /// Gets all action mappings for a specific project.
        /// Returns an array of ActionMappingDto objects.
        /// </summary>
        /// <param name="projectId">The GUID of the project</param>
        [HttpGet("action-mappings/{projectId}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult> GetActionMappings(Guid projectId)
        {
            try
            {
                var query = new GetActionMappingsByProjectQuery(projectId);
                var result = await _mediator.Send(query);

                if (result.Status == StatusCodes.Status200OK)
                    return Ok(result);

                return BadRequest(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting action mappings for project {ProjectId}", projectId);
                return StatusCode(StatusCodes.Status500InternalServerError, new ResponseHttp { Fail_Messages = "An error occurred while processing the request.", Status = StatusCodes.Status500InternalServerError });
            }
        }

        /// <summary>
        /// PUT /api/nlp/action-mappings/{mappingId}
        /// Updates an existing action mapping.
        /// </summary>
        [HttpPut("action-mappings/{mappingId}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult> UpdateActionMapping(
            Guid mappingId,
            [FromBody] CreateActionMappingRequest request)
        {
            try
            {
                if (IsViewerRole())
                    return ViewerWriteForbidden("update");

                var command = new UpdateActionMappingCommand(
                    mappingId,
                    request.IntentPattern,
                    request.ActionType,
                    request.SelectorStrategy,
                    request.SelectorValue,
                    request.Description,
                    request.Priority
                );
                var result = await _mediator.Send(command);

                if (result.Status == StatusCodes.Status404NotFound)
                    return NotFound(result);
                if (result.Status == StatusCodes.Status400BadRequest)
                    return BadRequest(result);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating action mapping {MappingId}", mappingId);
                return StatusCode(StatusCodes.Status500InternalServerError, new ResponseHttp { Fail_Messages = "An error occurred while processing the request.", Status = StatusCodes.Status500InternalServerError });
            }
        }

        /// <summary>
        /// DELETE /api/nlp/action-mappings/{mappingId}
        /// Soft-deletes an action mapping.
        /// </summary>
        [HttpDelete("action-mappings/{mappingId}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult> DeleteActionMapping(Guid mappingId)
        {
            try
            {
                if (IsViewerRole())
                    return ViewerWriteForbidden("delete");

                var command = new DeleteActionMappingCommand(mappingId);
                var result = await _mediator.Send(command);

                if (result.Status == StatusCodes.Status404NotFound)
                    return NotFound(result);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting action mapping {MappingId}", mappingId);
                return StatusCode(StatusCodes.Status500InternalServerError, new ResponseHttp { Fail_Messages = "An error occurred while processing the request.", Status = StatusCodes.Status500InternalServerError });
            }
        }

        /// <summary>
        /// PUT /api/nlp/action-mappings/{mappingId}/status
        /// Toggles the active/inactive status of an action mapping.
        /// </summary>
        [HttpPut("action-mappings/{mappingId}/status")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult> ToggleActionMappingStatus(
            Guid mappingId,
            [FromBody] ToggleStatusRequest request)
        {
            try
            {
                if (IsViewerRole())
                    return ViewerWriteForbidden("update");

                var command = new ToggleActionMappingStatusCommand(mappingId, request.IsActive);
                var result = await _mediator.Send(command);

                if (result.Status == StatusCodes.Status404NotFound)
                    return NotFound(result);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error toggling status for action mapping {MappingId}", mappingId);
                return StatusCode(StatusCodes.Status500InternalServerError, new ResponseHttp { Fail_Messages = "An error occurred while processing the request.", Status = StatusCodes.Status500InternalServerError });
            }
        }
    }

    public class ToggleStatusRequest
    {
        public bool IsActive { get; set; }
    }
}
