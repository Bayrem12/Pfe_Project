using Application.Features.ScenariosFeature.Commands;
using Application.Features.ScenariosFeature.DTOs;
using Application.Features.ScenariosFeature.Queries;
using Application.Features.ScenariosFeature.Validators;
using Application.Setting;
using Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers
{
    /// <summary>
    /// Gestion des scénarios Gherkin dans un projet
    /// </summary>
    [Authorize] // ← FIX SÉCURITÉ #1
    [Route("api/scenarios")]
    [ApiController]
    public class ScenariosController : ControllerBase
    {
        private readonly IMediator _mediator;

        public ScenariosController(IMediator mediator)
        {
            _mediator = mediator;
        }

        // ✅ FIX SÉCURITÉ #2 : CurrentUserId toujours actif
        private Guid CurrentUserId => Guid.Parse(
            User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? throw new UnauthorizedAccessException("User not authenticated"));

        #region Scenario Endpoints

        /// <summary>
        /// Récupérer la liste des scénarios (avec pagination)
        /// </summary>
        /// <param name="featureId">Identifiant de la feature (optionnel)</param>
        /// <param name="search">Mot-clé de recherche (optionnel)</param>
        /// <param name="status">Statut du scénario (optionnel)</param>
        /// <param name="pageNumber">Numéro de page (défaut: 1)</param>
        /// <param name="pageSize">Taille de page (défaut: 20)</param>
        /// <returns>Liste paginée des scénarios</returns>
        /// <response code="200">Liste récupérée avec succès</response>
        /// <response code="400">Requête invalide</response>
        /// <response code="401">Non authentifié</response>
        /// <response code="500">Erreur interne du serveur</response>
        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult> GetAll(
            [FromQuery] Guid? projectId,
            [FromQuery] Guid? featureId,
            [FromQuery] string? search,
            [FromQuery] ScenarioStatus? status,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 20)
        {
            try
            {
                // ✅ FIX #3: Pagination ajoutée
                var result = await _mediator.Send(new GetScenariosQuery(
                    projectId,
                    featureId,
                    search,
                    status,
                    pageNumber,
                    pageSize));
                return Ok(result);
            }
            catch (UnauthorizedAccessException)
            {
                return Unauthorized(new ResponseHttp
                {
                    Status = StatusCodes.Status401Unauthorized,
                    Fail_Messages = "You must be authenticated to access scenarios"
                });
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new ResponseHttp
                {
                    Status = StatusCodes.Status500InternalServerError,
                    Fail_Messages = ex.Message
                });
            }
        }

        /// <summary>
        /// Récupérer un scénario par son identifiant
        /// </summary>
        /// <param name="id">Identifiant du scénario</param>
        /// <returns>Détails du scénario</returns>
        /// <response code="200">Scénario trouvé</response>
        /// <response code="401">Non authentifié</response>
        /// <response code="403">Accès interdit</response>
        /// <response code="404">Scénario non trouvé</response>
        /// <response code="500">Erreur interne du serveur</response>
        [HttpGet("{id}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult> GetById(Guid id)
        {
            try
            {
                // ✅ FIX #4: Authorization check ajouté
                var result = await _mediator.Send(new GetScenarioByIdQuery(id, CurrentUserId));
                return Ok(result);
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new ResponseHttp
                {
                    Status = StatusCodes.Status403Forbidden,
                    Fail_Messages = ex.Message
                });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new ResponseHttp
                {
                    Status = StatusCodes.Status404NotFound,
                    Fail_Messages = ex.Message
                });
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new ResponseHttp
                {
                    Status = StatusCodes.Status500InternalServerError,
                    Fail_Messages = ex.Message
                });
            }
        }

        /// <summary>
        /// Créer un nouveau scénario Gherkin
        /// </summary>
        /// <param name="request">Informations du scénario</param>
        /// <returns>Scénario créé</returns>
        /// <response code="201">Scénario créé avec succès</response>
        /// <response code="400">Données invalides</response>
        /// <response code="401">Non authentifié</response>
        /// <response code="403">Accès interdit</response>
        /// <response code="500">Erreur interne du serveur</response>
        [HttpPost]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult> Create([FromBody] CreateScenarioRequest request)
        {
            try
            {
                var cmd = new CreateScenarioCommand(
                    request.FeatureId,
                    request.Title,
                    request.Description,
                    request.GherkinContent,
                    CurrentUserId, // ✅ FIX SÉCURITÉ #2: CurrentUserId utilisé
                    request.Status ?? ScenarioStatus.Draft,
                    request.Tags ?? new List<string>()
                );

                // Validation déplacée dans le handler (MediatR pipeline)
                var result = await _mediator.Send(cmd);

                if (result.Status == StatusCodes.Status403Forbidden)
                {
                    return StatusCode(StatusCodes.Status403Forbidden, result);
                }

                if (result.Status == StatusCodes.Status400BadRequest)
                {
                    return BadRequest(result);
                }

                return StatusCode(StatusCodes.Status201Created, result);
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new ResponseHttp
                {
                    Status = StatusCodes.Status403Forbidden,
                    Fail_Messages = ex.Message
                });
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new ResponseHttp
                {
                    Status = StatusCodes.Status500InternalServerError,
                    Fail_Messages = ex.Message
                });
            }
        }

        /// <summary>
        /// Mettre à jour un scénario existant
        /// </summary>
        /// <param name="id">Identifiant du scénario</param>
        /// <param name="request">Nouvelles informations du scénario</param>
        /// <returns>Scénario mis à jour</returns>
        /// <response code="200">Scénario mis à jour avec succès</response>
        /// <response code="400">Erreur dans les données</response>
        /// <response code="401">Non authentifié</response>
        /// <response code="403">Accès interdit</response>
        /// <response code="404">Scénario non trouvé</response>
        /// <response code="500">Erreur interne du serveur</response>
        [HttpPut("{id}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult> Update(Guid id, [FromBody] UpdateScenarioRequest request)
        {
            try
            {
                var cmd = new UpdateScenarioCommand(
                    id,
                    request.Title,
                    request.Description,
                    request.GherkinContent,
                    request.ChangeDescription,
                    CurrentUserId, // ✅ FIX SÉCURITÉ #2: CurrentUserId utilisé
                    request.Status,
                    request.Tags
                );

                var result = await _mediator.Send(cmd);

                if (result.Status == StatusCodes.Status403Forbidden)
                {
                    return StatusCode(StatusCodes.Status403Forbidden, result);
                }

                if (result.Status == StatusCodes.Status404NotFound)
                {
                    return NotFound(result);
                }

                if (result.Status == StatusCodes.Status400BadRequest)
                {
                    return BadRequest(result);
                }

                return Ok(result);
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new ResponseHttp
                {
                    Status = StatusCodes.Status403Forbidden,
                    Fail_Messages = ex.Message
                });
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new ResponseHttp
                {
                    Status = StatusCodes.Status500InternalServerError,
                    Fail_Messages = ex.Message
                });
            }
        }

        /// <summary>
        /// Supprimer un scénario
        /// </summary>
        /// <param name="id">Identifiant du scénario</param>
        /// <returns>Confirmation de suppression</returns>
        /// <response code="204">Scénario supprimé avec succès</response>
        /// <response code="401">Non authentifié</response>
        /// <response code="403">Accès interdit</response>
        /// <response code="404">Scénario non trouvé</response>
        /// <response code="500">Erreur interne du serveur</response>
        [HttpDelete("{id}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult> Delete(Guid id)
        {
            try
            {
                // ✅ FIX SÉCURITÉ #3: Authorization check ajouté
                var result = await _mediator.Send(new DeleteScenarioCommand(id, CurrentUserId));

                if (result.Status == StatusCodes.Status403Forbidden)
                {
                    return StatusCode(StatusCodes.Status403Forbidden, result);
                }

                if (result.Status == StatusCodes.Status404NotFound)
                {
                    return NotFound(result);
                }

                return NoContent();
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new ResponseHttp
                {
                    Status = StatusCodes.Status403Forbidden,
                    Fail_Messages = ex.Message
                });
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new ResponseHttp
                {
                    Status = StatusCodes.Status500InternalServerError,
                    Fail_Messages = ex.Message
                });
            }
        }

        #endregion

        #region Import / Validation / Export Endpoints

        /// <summary>
        /// Valider la syntaxe Gherkin d'un scénario
        /// </summary>
        /// <param name="request">Contenu Gherkin à valider</param>
        /// <returns>Résultat de validation</returns>
        /// <response code="200">Validation effectuée</response>
        /// <response code="400">Erreur dans la requête</response>
        /// <response code="401">Non authentifié</response>
        /// <response code="500">Erreur interne du serveur</response>
        [HttpPost("validate")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult> Validate([FromBody] ValidateGherkinRequest request)
        {
            try
            {
                var result = await _mediator.Send(new ValidateGherkinCommand(request.GherkinContent));
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new ResponseHttp
                {
                    Status = StatusCodes.Status500InternalServerError,
                    Fail_Messages = ex.Message
                });
            }
        }

        /// <summary>
        /// Exporter un scénario au format Gherkin
        /// </summary>
        /// <param name="id">Identifiant du scénario</param>
        /// <returns>Contenu du scénario au format Gherkin</returns>
        /// <response code="200">Export réussi</response>
        /// <response code="401">Non authentifié</response>
        /// <response code="403">Accès interdit</response>
        /// <response code="404">Scénario non trouvé</response>
        /// <response code="500">Erreur interne du serveur</response>
        [HttpGet("{id}/export")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult> Export(Guid id)
        {
            try
            {
                var result = await _mediator.Send(new ExportScenarioQuery(id, CurrentUserId));

                if (result.Status == StatusCodes.Status403Forbidden)
                {
                    return StatusCode(StatusCodes.Status403Forbidden, result);
                }

                if (result.Status == StatusCodes.Status404NotFound)
                {
                    return NotFound(result);
                }

                return Ok(result);
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new ResponseHttp
                {
                    Status = StatusCodes.Status403Forbidden,
                    Fail_Messages = ex.Message
                });
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new ResponseHttp
                {
                    Status = StatusCodes.Status500InternalServerError,
                    Fail_Messages = ex.Message
                });
            }
        }

        #endregion
    }

    #region Request Models

    public class CreateScenarioRequest
    {
        public Guid FeatureId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string GherkinContent { get; set; } = string.Empty;
        public ScenarioStatus? Status { get; set; }
        public List<string>? Tags { get; set; }
    }

    public class UpdateScenarioRequest
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string GherkinContent { get; set; } = string.Empty;
        public string? ChangeDescription { get; set; }
        public ScenarioStatus? Status { get; set; }
        public List<string>? Tags { get; set; }
    }

    public class ValidateGherkinRequest
    {
        public string GherkinContent { get; set; } = string.Empty;
    }

    #endregion
}