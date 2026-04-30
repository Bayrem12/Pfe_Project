using Application.Features.TagsFeature.Commands;
using Application.Features.TagsFeature.Queries;
using Application.Features.TagsFeature.Validators;
using Application.Interfaces;
using Application.Interfaces.Repositories;
using Application.Setting;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace API.Controllers
{
    /// <summary>
    /// Gestion des tags d'un projet
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class TagsController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly IProjectRepository _projectRepository;
        private readonly ITagsRepository _tagsRepository;

        public TagsController(IMediator mediator, IProjectRepository projectRepository, ITagsRepository tagsRepository)
        {
            _mediator = mediator;
            _projectRepository = projectRepository;
            _tagsRepository = tagsRepository;
        }

        private async Task<bool> CurrentUserBelongsToProjectAsync(Guid projectId, CancellationToken cancellationToken)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdClaim, out var userId))
            {
                return false;
            }

            var member = await _projectRepository.GetProjectMemberAsync(projectId, userId, cancellationToken);
            return member != null;
        }

        /// <summary>
        /// Récupérer tous les tags associés à un projet
        /// </summary>
        /// <param name="projectId">Identifiant du projet</param>
        /// <returns>Liste des tags du projet</returns>
        [HttpGet("by-project/{projectId}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetByProjectId(Guid projectId)
        {
            if (!await CurrentUserBelongsToProjectAsync(projectId, HttpContext.RequestAborted))
            {
                return StatusCode(StatusCodes.Status403Forbidden, new ResponseHttp
                {
                    Fail_Messages = "Access denied.",
                    Status = StatusCodes.Status403Forbidden
                });
            }

            var result = await _mediator.Send(new GetTagsByProjectIdQuery(projectId));
            return Ok(new ResponseHttp { Resultat = result, Status = 200 });
        }

        /// <summary>
        /// Créer un nouveau tag dans un projet
        /// </summary>
        /// <param name="command">Informations nécessaires pour créer un tag</param>
        /// <returns>Identifiant du tag créé</returns>
        [HttpPost]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> Create([FromBody] AddTagCommand command)
        {
            try
            {
                var validator = new AddTagCommandValidator();
                var validationResult = validator.Validate(new ValidationContext<AddTagCommand>(command));
                if (validationResult.Status == StatusCodes.Status400BadRequest)
                {
                    return BadRequest(validationResult);
                }

                if (!await CurrentUserBelongsToProjectAsync(command.ProjectId, HttpContext.RequestAborted))
                {
                    return StatusCode(StatusCodes.Status403Forbidden, new ResponseHttp
                    {
                        Fail_Messages = "Access denied.",
                        Status = StatusCodes.Status403Forbidden
                    });
                }

                var tagId = await _mediator.Send(command);

                var tagResult = new
                {
                    id = tagId,
                    projectId = command.ProjectId,
                    name = command.Name,
                    color = command.Color ?? "#6366F1",
                    description = command.Description
                };

                return Ok(new ResponseHttp { Resultat = tagResult, Status = 200 });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new ResponseHttp { Fail_Messages = ex.Message, Status = 400 });
            }
        }

        /// <summary>
        /// Supprimer un tag
        /// </summary>
        /// <param name="id">Identifiant du tag</param>
        [HttpDelete("{id}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> Delete(Guid id)
        {
            var tag = await _tagsRepository.GetByIdAsync(id, HttpContext.RequestAborted);
            if (tag == null)
            {
                return NotFound(new ResponseHttp
                {
                    Fail_Messages = "Tag not found.",
                    Status = StatusCodes.Status404NotFound
                });
            }

            if (!await CurrentUserBelongsToProjectAsync(tag.ProjectId, HttpContext.RequestAborted))
            {
                return StatusCode(StatusCodes.Status403Forbidden, new ResponseHttp
                {
                    Fail_Messages = "Access denied.",
                    Status = StatusCodes.Status403Forbidden
                });
            }

            var deleted = await _mediator.Send(new DeleteTagCommand(id));

            if (!deleted)
            {
                return NotFound(new ResponseHttp
                {
                    Fail_Messages = "Tag not found.",
                    Status = StatusCodes.Status404NotFound
                });
            }

            return StatusCode(StatusCodes.Status204NoContent, new ResponseHttp
            {
                Status = StatusCodes.Status204NoContent
            });
        }
    }
}
