using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Application.Features.ModulesFeature.Commands;
using Application.Setting;

using Asp.Versioning;
namespace API.Controllers
{
    /// <summary>
    /// Gestion des modules d'un projet
    /// </summary>
    [Route("api/modules")]
    [ApiVersion("1.0")]
    [ApiController]
    [Authorize] // Fix : tous les endpoints modules nécessitent une authentification
    public class ModulesController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly ILogger<ModulesController> _logger;

        public ModulesController(IMediator mediator, ILogger<ModulesController> logger)
        {
            _mediator = mediator;
            _logger = logger;
        }

        /// <summary>
        /// Récupérer tous les modules associés à un projet
        /// </summary>
        [HttpGet("by-project/{projectId}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetByProjectId(Guid projectId)
        {
            var result = await _mediator.Send(new GetModulesByProjectIdQuery(projectId));

            return Ok(new ResponseHttp
            {
                Resultat = result,
                Status = StatusCodes.Status200OK
            });
        }

        /// <summary>
        /// Créer un nouveau module dans un projet
        /// </summary>
        [HttpPost]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> Create([FromBody] AddModulesCommand command)
        {
            var moduleId = await _mediator.Send(command);

            return Created($"api/modules/{moduleId}", moduleId);
        }

        /// <summary>
        /// Supprimer (soft delete) un module.
        /// </summary>
        [HttpDelete("{id}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> Delete(Guid id)
        {
            var result = await _mediator.Send(new DeleteModuleCommand(id));
            return Ok(result);
        }
    }
}