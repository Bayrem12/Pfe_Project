using Application.Features.ProjectFeature.Commands;
using Application.Features.ProjectFeature.Dtos;
using Application.Features.ProjectFeature.Queries;
using Application.Features.ProjectFeature.Validators;
using Application.Setting;
using Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Persistance.Data;
using System.Security.Claims;

using Asp.Versioning;
namespace API.Controllers
{
    /// <summary>
    /// Gestion des projets et des membres des projets
    /// </summary>
    [Route("api/projet")]
    [ApiVersion("1.0")]
    [ApiController]
    [Authorize]
    public class ProjectsController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly TestAutoumatisationContext _dbContext;
        private readonly ILogger<ProjectsController> _logger;

        public ProjectsController(IMediator mediator, TestAutoumatisationContext dbContext, ILogger<ProjectsController> logger)
        {
            _mediator = mediator;
            _dbContext = dbContext;
            _logger = logger;
        }

        private Guid CurrentUserId => Guid.TryParse(
            User.FindFirst(ClaimTypes.NameIdentifier)?.Value,
            out var userId)
            ? userId
            : Guid.Empty;

        private async Task<string> GetCurrentGlobalRoleAsync()
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

        private async Task<bool> CanReadProjectAsync(Guid projectId)
        {
            var role = await GetCurrentGlobalRoleAsync();

            if (role == "admin")
            {
                return true;
            }

            // Manager, Tester, Viewer: only member projects
            return await _dbContext.ProjectMembers
                .AsNoTracking()
                .AnyAsync(m => !m.IsDeleted && m.ProjectId == projectId && m.UserId == CurrentUserId);
        }

        private async Task<bool> CanWriteProjectAsync(Guid? projectId = null)
        {
            var role = await GetCurrentGlobalRoleAsync();

            if (role == "admin")
            {
                return true;
            }

            if (role == "manager")
            {
                if (!projectId.HasValue)
                {
                    // Manager can create new projects
                    return true;
                }

                // Manager can update/delete projects they are a member of
                return await _dbContext.ProjectMembers
                    .AsNoTracking()
                    .AnyAsync(m => !m.IsDeleted && m.ProjectId == projectId.Value && m.UserId == CurrentUserId);
            }

            // Tester and Viewer: cannot create or modify projects
            return false;
        }

        // Admin and Manager (if member) can manage project members
        private async Task<bool> CanManageMembersAsync(Guid projectId)
        {
            var role = await GetCurrentGlobalRoleAsync();

            if (role == "admin")
            {
                return true;
            }

            if (role == "manager")
            {
                return await _dbContext.ProjectMembers
                    .AsNoTracking()
                    .AnyAsync(m => !m.IsDeleted && m.ProjectId == projectId && m.UserId == CurrentUserId);
            }

            return false;
        }

        // Manager and Tester (if member) can run executions
        private async Task<bool> CanRunExecutionAsync(Guid projectId)
        {
            var role = await GetCurrentGlobalRoleAsync();
            if (role == "admin") return true;
            if (role == "manager" || role == "tester")
            {
                return await _dbContext.ProjectMembers
                    .AsNoTracking()
                    .AnyAsync(m => !m.IsDeleted && m.ProjectId == projectId && m.UserId == CurrentUserId);
            }
            return false;
        }

        #region Project Endpoints

        /// <summary>
        /// Récupérer la liste des projets de l'utilisateur authentifié
        /// </summary>
        /// <param name="pageNumber">Numéro de page (optionnel)</param>
        /// <param name="pageSize">Taille de page (optionnel)</param>
        /// <returns>Liste des projets</returns>
        /// <response code="200">Liste récupérée avec succès</response>
        /// <response code="400">Requête invalide</response>
        /// <response code="500">Erreur interne du serveur</response>
        [HttpGet("")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult> GetAll(int? pageNumber, int? pageSize)
        {
            try
            {
                if (CurrentUserId == Guid.Empty)
                {
                    return Unauthorized("Invalid user context");
                }

                var result = await _mediator.Send(new GetAllProjectsQuery(CurrentUserId, pageNumber, pageSize));
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error processing request.");
                return BadRequest("An unexpected error occurred.");
            }
        }

        /// <summary>
        /// R�cup�rer un projet par son identifiant
        /// </summary>
        /// <param name="id">Identifiant du projet</param>
        /// <returns>D�tails du projet</returns>
        /// <response code="200">Projet trouv�</response>
        /// <response code="400">Erreur dans la requ�te</response>
        /// <response code="500">Erreur interne du serveur</response>
        [HttpGet("{id}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult> GetById(Guid id)
        {
            try
            {
                if (!await CanReadProjectAsync(id))
                {
                    return StatusCode(StatusCodes.Status403Forbidden, "Access denied to this project.");
                }

                var result = await _mediator.Send(new GetProjectByIdQuery(id));
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error processing request.");
                return BadRequest("An unexpected error occurred.");
            }
        }

        /// <summary>
        /// Cr�er un nouveau projet
        /// </summary>
        /// <param name="cmd">Informations du projet</param>
        /// <returns>Projet cr��</returns>
        /// <response code="200">Projet cr�� avec succ�s</response>
        /// <response code="400">Donn�es invalides</response>
        /// <response code="500">Erreur interne du serveur</response>
        [HttpPost]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult> Add([FromBody] AddProjectCommand cmd)
        {
            try
            {
                if (!await CanWriteProjectAsync())
                {
                    return StatusCode(StatusCodes.Status403Forbidden, "You are not allowed to create projects.");
                }

                var role = await GetCurrentGlobalRoleAsync();

                // Force creator to authenticated user for security and consistency.
                // Also pass the global role so the handler knows not to add Admin as a project member.
                cmd = cmd with { UserId = CurrentUserId, GlobalRole = role };

                ResponseHttp result;
                AddProjectCommandValidator validator = new();

                result = validator.Validate(new ValidationContext<AddProjectCommand>(cmd));

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
        /// Mettre � jour un projet
        /// </summary>
        /// <param name="id">Identifiant du projet</param>
        /// <param name="cmd">Nouvelles informations du projet</param>
        /// <returns>Projet mis � jour</returns>
        /// <response code="200">Projet mis � jour avec succ�s</response>
        /// <response code="400">Erreur dans les donn�es</response>
        /// <response code="500">Erreur interne du serveur</response>
        [HttpPut("{id}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult> Update(Guid id, [FromBody] UpdateProjectCommand cmd)
        {
            try
            {
                if (!await CanWriteProjectAsync(id))
                {
                    return StatusCode(StatusCodes.Status403Forbidden, "You are not allowed to update this project.");
                }

                if (id != cmd.ProjectId)
                {
                    return BadRequest("Project ID mismatch");
                }

                ResponseHttp result;
                UpdateProjectCommandValidator validator = new();

                result = validator.Validate(new ValidationContext<UpdateProjectCommand>(cmd));

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
        /// Supprimer un projet
        /// </summary>
        /// <param name="id">Identifiant du projet</param>
        /// <returns>Confirmation de suppression</returns>
        /// <response code="200">Projet supprim� avec succ�s</response>
        /// <response code="400">Requ�te invalide</response>
        /// <response code="500">Erreur interne du serveur</response>
        [HttpDelete("{id}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult> Delete(Guid id)
        {
            try
            {
                if (!await CanWriteProjectAsync(id))
                {
                    return StatusCode(StatusCodes.Status403Forbidden, "You are not allowed to delete this project.");
                }

                var result = await _mediator.Send(new DeleteProjectCommand(id));
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error processing request.");
                return BadRequest("An unexpected error occurred.");
            }
        }

        #endregion

        #region Project Members Endpoints

        /// <summary>
        /// R�cup�rer la liste des membres d'un projet
        /// </summary>
        /// <param name="projectId">Identifiant du projet</param>
        /// <returns>Liste des membres</returns>
        /// <response code="200">Liste r�cup�r�e avec succ�s</response>
        /// <response code="400">Erreur dans la requ�te</response>
        /// <response code="500">Erreur interne du serveur</response>
        [HttpGet("{projectId}/members")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult> GetMembers(Guid projectId)
        {
            try
            {
                if (!await CanReadProjectAsync(projectId))
                {
                    return StatusCode(StatusCodes.Status403Forbidden, "Access denied to project members.");
                }

                var result = await _mediator.Send(new GetProjectMembersQuery(projectId));
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error processing request.");
                return BadRequest("An unexpected error occurred.");
            }
        }

        /// <summary>
        /// Ajouter un membre � un projet
        /// </summary>
        /// <param name="projectId">Identifiant du projet</param>
        /// <param name="request">Informations du membre</param>
        /// <returns>Membre ajout�</returns>
        /// <response code="200">Membre ajout� avec succ�s</response>
        /// <response code="400">Donn�es invalides</response>
        /// <response code="500">Erreur interne du serveur</response>
        [HttpPost("{projectId}/members")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult> AddMember(Guid projectId, [FromBody] AddProjectMemberRequest request)
        {
            try
            {
                if (!await CanManageMembersAsync(projectId))
                {
                    return StatusCode(StatusCodes.Status403Forbidden, "You are not allowed to add members to this project.");
                }

                var cmd = new AddProjectMemberCommand(projectId, request.UserId, request.Role.ToString());

                ResponseHttp result;
                AddProjectMemberCommandValidator validator = new();

                result = validator.Validate(new ValidationContext<AddProjectMemberCommand>(cmd));

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
        /// Supprimer un membre d'un projet
        /// </summary>
        /// <param name="projectId">Identifiant du projet</param>
        /// <param name="userId">Identifiant de l'utilisateur</param>
        /// <returns>Membre supprim�</returns>
        /// <response code="200">Membre supprim� avec succ�s</response>
        /// <response code="400">Requ�te invalide</response>
        /// <response code="500">Erreur interne du serveur</response>
        [HttpDelete("{projectId}/members/{userId}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult> RemoveMember(Guid projectId, Guid userId)
        {
            try
            {
                if (!await CanManageMembersAsync(projectId))
                {
                    return StatusCode(StatusCodes.Status403Forbidden, "You are not allowed to remove members from this project.");
                }

                var result = await _mediator.Send(new RemoveProjectMemberCommand(projectId, userId));
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error processing request.");
                return BadRequest("An unexpected error occurred.");
            }
        }

        #endregion
    }
}
