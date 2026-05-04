using Application.Features.UserFeature.Commands;
using Application.Features.UserFeature.DTOs;
using Application.Features.UserFeature.Queries;
using Application.Features.UserFeature.Validators;
using Application.Setting;
using Application.Users.DTOs;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

using Asp.Versioning;
namespace API.Controllers
{
    [Route("api/user")]
    [ApiVersion("1.0")]
    [ApiController]
    [Authorize]
    public class UserController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly ILogger<UserController> _logger;

        public UserController(IMediator mediator, ILogger<UserController> logger)
        {
            _mediator = mediator;
            _logger = logger;
        }

        private async Task<bool> CurrentUserIsAdminAsync()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdClaim, out var userId))
            {
                return false;
            }

            var currentUser = await _mediator.Send(new GetUserByIdQuery(userId));
            return currentUser?.Roles.Any(role => string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase)) == true;
        }

        [HttpGet]
        public async Task<ActionResult> GetAllUsers()
        {
            try
            {
                if (!await CurrentUserIsAdminAsync())
                {
                    return StatusCode(StatusCodes.Status403Forbidden, new ResponseHttp
                    {
                        FailMessages = "Access denied.",
                        Status = StatusCodes.Status403Forbidden
                    });
                }

                var result = await _mediator.Send(new GetUsersQuery());
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error processing request.");
                return BadRequest(new ResponseHttp
                {
                    FailMessages = "An unexpected error occurred.",
                    Status = StatusCodes.Status400BadRequest
                });
            }
        }

        [HttpGet("{id}")]
        public async Task<ActionResult> GetUserById(Guid id)
        {
            try
            {
                var result = await _mediator.Send(new GetUserByIdQuery(id));
                if (result == null)
                    return NotFound(new ResponseHttp
                    {
                        FailMessages = "Utilisateur introuvable",
                        Status = StatusCodes.Status404NotFound
                    });

                return Ok(new ResponseHttp
                {
                    Resultat = result,
                    Status = StatusCodes.Status200OK
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error processing request.");
                return BadRequest(new ResponseHttp
                {
                    FailMessages = "An unexpected error occurred.",
                    Status = StatusCodes.Status400BadRequest
                });
            }
        }

        [HttpGet("search")]
        public async Task<ActionResult> SearchUsers([FromQuery] string? keyword)
        {
            try
            {
                var result = await _mediator.Send(new GetSearchUserQuery(keyword ?? string.Empty));
                return Ok(new ResponseHttp
                {
                    Resultat = result,
                    Status = StatusCodes.Status200OK
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error processing request.");
                return BadRequest(new ResponseHttp
                {
                    FailMessages = "An unexpected error occurred.",
                    Status = StatusCodes.Status400BadRequest
                });
            }
        }

        [HttpPut("{id}")]
        public async Task<ActionResult> UpdateUser(Guid id, [FromBody] UserDto dto)
        {
            try
            {
                if (!await CurrentUserIsAdminAsync())
                {
                    return StatusCode(StatusCodes.Status403Forbidden, new ResponseHttp
                    {
                        FailMessages = "Access denied.",
                        Status = StatusCodes.Status403Forbidden
                    });
                }

                var validator = new UserDtoValidator();
                var validationResult = validator.Validate(dto);

                if (!validationResult.IsValid)
                    return BadRequest(new ResponseHttp
                    {
                        FailMessages = string.Join(", ", validationResult.Errors.Select(e => e.ErrorMessage)),
                        Status = StatusCodes.Status400BadRequest
                    });

                var cmd = new UpdateUserByIdCommand(id, dto.FirstName, dto.LastName, dto.Email, dto.IsActive);
                var result = await _mediator.Send(cmd);
                return Ok(result); // ResponseHttp déjà retourné par le handler
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error processing request.");
                return BadRequest(new ResponseHttp
                {
                    FailMessages = "An unexpected error occurred.",
                    Status = StatusCodes.Status400BadRequest
                });
            }
        }

        [HttpPut("{id}/roles")]
        public async Task<ActionResult> UpdateUserRoles(Guid id, [FromBody] UpdateUserRolesDto dto)
        {
            try
            {
                var validator = new UpdateUserRolesDtoValidator();
                var validationResult = validator.Validate(dto);

                if (!validationResult.IsValid)
                    return BadRequest(new ResponseHttp
                    {
                        FailMessages = string.Join(", ", validationResult.Errors.Select(e => e.ErrorMessage)),
                        Status = StatusCodes.Status400BadRequest
                    });

                var cmd = new UpdateUserRolesByIdCommand(id, dto.Roles);
                var result = await _mediator.Send(cmd);
                return Ok(result); // ResponseHttp déjà retourné par le handler
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error processing request.");
                return BadRequest(new ResponseHttp
                {
                    FailMessages = "An unexpected error occurred.",
                    Status = StatusCodes.Status400BadRequest
                });
            }
        }

        [HttpPost("{id}/toggle-status")]
        public async Task<ActionResult> ToggleUserStatus(Guid id)
        {
            try
            {
                var cmd = new AddToggleUserStatusCommand(id);
                var result = await _mediator.Send(cmd);
                return Ok(result); // ResponseHttp déjà retourné par le handler
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error processing request.");
                return BadRequest(new ResponseHttp
                {
                    FailMessages = "An unexpected error occurred.",
                    Status = StatusCodes.Status400BadRequest
                });
            }
        }

        [HttpDelete("{id}")]
        public async Task<ActionResult> DeleteUser(Guid id)
        {
            try
            {
                if (!await CurrentUserIsAdminAsync())
                {
                    return StatusCode(StatusCodes.Status403Forbidden, new ResponseHttp
                    {
                        FailMessages = "Access denied.",
                        Status = StatusCodes.Status403Forbidden
                    });
                }

                var result = await _mediator.Send(new DeleteUserByIdCommand(id));
                return StatusCode(result.Status, result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error processing request.");
                return BadRequest(new ResponseHttp
                {
                    FailMessages = "An unexpected error occurred.",
                    Status = StatusCodes.Status400BadRequest
                });
            }
        }
    }
}
