using Application.Features.FeatureFeature.Commands;
using Application.Features.FeatureFeature.Dtos;
using Application.Features.FeatureFeature.Queries;
using Application.Features.FeatureFeature.Validators;
using Application.Setting;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Mvc;

using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
namespace API.Controllers
{
    /// <summary>
    /// Gestion des features dans un module
    /// </summary>
    [Authorize]
    [Route("api/features")]
    [ApiVersion("1.0")]
    [ApiController]
    public class FeaturesController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly ILogger<FeaturesController> _logger;

        public FeaturesController(IMediator mediator, ILogger<FeaturesController> logger)
        {
            _mediator = mediator;
            _logger = logger;
        }

        private Guid CurrentUserId => Guid.Parse(
            User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? Guid.Empty.ToString());

        /// <summary>
        /// Récupérer toutes les features d'un module
        /// </summary>
        /// <param name="moduleId">Identifiant du module</param>
        /// <returns>Liste des features</returns>
        /// <response code="200">Liste récupérée avec succès</response>
        /// <response code="400">Requête invalide</response>
        /// <response code="500">Erreur interne du serveur</response>
        [HttpGet("by-module/{moduleId}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult> GetByModuleId(Guid moduleId)
        {
            try
            {
                var result = await _mediator.Send(new GetFeaturesByModuleQuery(moduleId));
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error processing request.");
                return BadRequest("An unexpected error occurred.");
            }
        }

        /// <summary>
        /// Récupérer une feature par son identifiant
        /// </summary>
        /// <param name="id">Identifiant de la feature</param>
        /// <returns>Détails de la feature</returns>
        /// <response code="200">Feature trouvée</response>
        /// <response code="400">Erreur dans la requête</response>
        /// <response code="500">Erreur interne du serveur</response>
        [HttpGet("{id}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult> GetById(Guid id)
        {
            try
            {
                var result = await _mediator.Send(new GetFeatureByIdQuery(id));
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error processing request.");
                return BadRequest("An unexpected error occurred.");
            }
        }

        /// <summary>
        /// Créer une nouvelle feature dans un module
        /// </summary>
        /// <param name="request">Informations de la feature</param>
        /// <returns>Feature créée</returns>
        /// <response code="200">Feature créée avec succès</response>
        /// <response code="400">Données invalides</response>
        /// <response code="500">Erreur interne du serveur</response>
        [HttpPost]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult> Add([FromBody] AddFeatureRequest request)
        {
            try
            {
                var cmd = new AddFeatureCommand(
                    request.ModuleId,
                    request.Name,
                    request.Description,
                    request.DisplayOrder,
                    CurrentUserId
                );

                ResponseHttp result;
                AddFeatureCommandValidator validator = new();

                result = validator.Validate(new ValidationContext<AddFeatureCommand>(cmd));

                if (result.Status == StatusCodes.Status400BadRequest)
                {
                    return BadRequest(result);
                }

                result = await _mediator.Send(cmd);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error processing request.");
                return BadRequest("An unexpected error occurred.");
            }
        }

        /// <summary>
        /// Mettre à jour une feature
        /// </summary>
        /// <param name="id">Identifiant de la feature</param>
        /// <param name="request">Nouvelles informations de la feature</param>
        /// <returns>Feature mise à jour</returns>
        /// <response code="200">Feature mise à jour avec succès</response>
        /// <response code="400">Erreur dans les données</response>
        /// <response code="500">Erreur interne du serveur</response>
        [HttpPut("{id}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult> Update(Guid id, [FromBody] UpdateFeatureRequest request)
        {
            try
            {
                var cmd = new UpdateFeatureCommand(
                    id,
                    request.Name,
                    request.Description,
                    request.DisplayOrder,
                    CurrentUserId
                );

                ResponseHttp result;
                UpdateFeatureCommandValidator validator = new();

                result = validator.Validate(new ValidationContext<UpdateFeatureCommand>(cmd));

                if (result.Status == StatusCodes.Status400BadRequest)
                {
                    return BadRequest(result);
                }

                result = await _mediator.Send(cmd);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error processing request.");
                return BadRequest("An unexpected error occurred.");
            }
        }

        /// <summary>
        /// Supprimer une feature
        /// </summary>
        /// <param name="id">Identifiant de la feature</param>
        /// <returns>Confirmation de suppression</returns>
        /// <response code="200">Feature supprimée avec succès</response>
        /// <response code="400">Requête invalide</response>
        /// <response code="500">Erreur interne du serveur</response>
        [HttpDelete("{id}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult> Delete(Guid id)
        {
            try
            {
                var result = await _mediator.Send(new DeleteFeatureCommand(id));
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error processing request.");
                return BadRequest("An unexpected error occurred.");
            }
        }

    }
   
}