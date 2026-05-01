using Application.Features.AuthFeature.Commands;
using Application.Features.AuthFeature.Validators;
using Application.Features.UserFeature.Commands;
using Application.Features.UserFeature.Validators;
using Application.Setting;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

using Asp.Versioning;
namespace API.Controllers
{
    public record OAuthCallbackRequest(string Code, string RedirectUri);

    /// <summary>
    /// Gestion de l'authentification des utilisateurs
    /// </summary>
    [ApiVersion("1.0")]
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly ILogger<AuthController> _logger;

        public AuthController(IMediator mediator, ILogger<AuthController> logger)
        {
            _mediator = mediator;
            _logger = logger;
        }

        /// <summary>
        /// Enregistrer un nouvel utilisateur
        /// </summary>
        [HttpPost("register")]
        [ProducesResponseType(typeof(ResponseHttp), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ResponseHttp), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ResponseHttp), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ResponseHttp>> Register(AddUserRegister cmd)
        {
            try
            {
                var validator = new AddUserRegisterValidator();
                var validationResult = validator.Validate(cmd);

                if (!validationResult.IsValid)
                    return StatusCode(StatusCodes.Status400BadRequest, new ResponseHttp
                    {
                        Status = StatusCodes.Status400BadRequest,
                        FailMessages = string.Join(" | ", validationResult.Errors.Select(e => e.ErrorMessage))
                    });

                var result = await _mediator.Send(cmd);
                return StatusCode(result.Status, result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error processing request.");
                return StatusCode(StatusCodes.Status500InternalServerError, new ResponseHttp
                {
                    Status = StatusCodes.Status500InternalServerError,
                    FailMessages = "An unexpected error occurred.",
                });
            }
        }

        /// <summary>
        /// Authentifier un utilisateur (connexion)
        /// ✅ Rate limiting : 5 tentatives par minute
        /// </summary>
        [HttpPost("login")]
        [EnableRateLimiting("login")]
        [ProducesResponseType(typeof(ResponseHttp), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ResponseHttp), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ResponseHttp), StatusCodes.Status429TooManyRequests)]
        [ProducesResponseType(typeof(ResponseHttp), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ResponseHttp>> Login(AddUserLogin cmd)
        {
            try
            {
                var validator = new AddUserLoginValidator();
                var validationResult = validator.Validate(cmd);

                if (!validationResult.IsValid)
                    return StatusCode(StatusCodes.Status400BadRequest, new ResponseHttp
                    {
                        Status = StatusCodes.Status400BadRequest,
                        FailMessages = string.Join(" | ", validationResult.Errors.Select(e => e.ErrorMessage))
                    });

                var result = await _mediator.Send(cmd);
                return StatusCode(result.Status, result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error processing request.");
                return StatusCode(StatusCodes.Status500InternalServerError, new ResponseHttp
                {
                    Status = StatusCodes.Status500InternalServerError,
                    FailMessages = "An unexpected error occurred.",
                });
            }
        }

        /// <summary>
        /// Rafraîchir l'access token via un refresh token opaque.
        /// ✅ Critique 2 — refresh token avec rotation implémenté.
        /// </summary>
        [HttpPost("refresh")]
        [ProducesResponseType(typeof(ResponseHttp), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ResponseHttp), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ResponseHttp), StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<ResponseHttp>> Refresh(AddUserRefresh cmd)
        {
            try
            {
                var validator = new AddUserRefreshValidator();
                var validationResult = validator.Validate(cmd);

                if (!validationResult.IsValid)
                    return StatusCode(StatusCodes.Status400BadRequest, new ResponseHttp
                    {
                        Status = StatusCodes.Status400BadRequest,
                        FailMessages = string.Join(" | ", validationResult.Errors.Select(e => e.ErrorMessage))
                    });

                var result = await _mediator.Send(cmd);
                return StatusCode(result.Status, result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error processing request.");
                return StatusCode(StatusCodes.Status500InternalServerError, new ResponseHttp
                {
                    Status = StatusCodes.Status500InternalServerError,
                    FailMessages = "An unexpected error occurred.",
                });
            }
        }

        /// <summary>
        /// Changer le mot de passe d'un utilisateur
        /// </summary>
        [HttpPost("change-password")]
        [ProducesResponseType(typeof(ResponseHttp), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ResponseHttp), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ResponseHttp), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ResponseHttp>> ChangePassword(AddUserChangePassword cmd)
        {
            try
            {
                var validator = new AddUserChangePasswordValidator();
                var validationResult = validator.Validate(cmd);

                if (!validationResult.IsValid)
                    return StatusCode(StatusCodes.Status400BadRequest, new ResponseHttp
                    {
                        Status = StatusCodes.Status400BadRequest,
                        FailMessages = string.Join(" | ", validationResult.Errors.Select(e => e.ErrorMessage))
                    });

                var result = await _mediator.Send(cmd);
                return StatusCode(result.Status, result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error processing request.");
                return StatusCode(StatusCodes.Status500InternalServerError, new ResponseHttp
                {
                    Status = StatusCodes.Status500InternalServerError,
                    FailMessages = "An unexpected error occurred.",
                });
            }
        }

        /// <summary>
        /// Demander un lien de réinitialisation de mot de passe
        /// </summary>
        [HttpPost("forgot-password")]
        [ProducesResponseType(typeof(ResponseHttp), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ResponseHttp), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ResponseHttp), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ResponseHttp>> ForgotPassword(AddForgotPassword cmd)
        {
            try
            {
                var validator = new AddForgotPasswordValidator();
                var validationResult = validator.Validate(cmd);

                if (!validationResult.IsValid)
                    return StatusCode(StatusCodes.Status400BadRequest, new ResponseHttp
                    {
                        Status = StatusCodes.Status400BadRequest,
                        FailMessages = string.Join(" | ", validationResult.Errors.Select(e => e.ErrorMessage))
                    });

                var result = await _mediator.Send(cmd);
                return StatusCode(result.Status, result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error processing request.");
                return StatusCode(StatusCodes.Status500InternalServerError, new ResponseHttp
                {
                    Status = StatusCodes.Status500InternalServerError,
                    FailMessages = "An unexpected error occurred.",
                });
            }
        }

        /// <summary>
        /// Réinitialiser le mot de passe avec le token reçu
        /// </summary>
        [HttpPost("reset-password")]
        [ProducesResponseType(typeof(ResponseHttp), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ResponseHttp), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ResponseHttp), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ResponseHttp>> ResetPassword(AddResetPassword cmd)
        {
            try
            {
                var validator = new AddResetPasswordValidator();
                var validationResult = validator.Validate(cmd);

                if (!validationResult.IsValid)
                    return StatusCode(StatusCodes.Status400BadRequest, new ResponseHttp
                    {
                        Status = StatusCodes.Status400BadRequest,
                        FailMessages = string.Join(" | ", validationResult.Errors.Select(e => e.ErrorMessage))
                    });

                var result = await _mediator.Send(cmd);
                return StatusCode(result.Status, result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error processing request.");
                return StatusCode(StatusCodes.Status500InternalServerError, new ResponseHttp
                {
                    Status = StatusCodes.Status500InternalServerError,
                    FailMessages = "An unexpected error occurred.",
                });
            }
        }

        /// <summary>
        /// Callback OAuth Google
        /// </summary>
        [HttpPost("google/callback")]
        [ProducesResponseType(typeof(ResponseHttp), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ResponseHttp), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ResponseHttp), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ResponseHttp>> GoogleCallback(
            [FromBody] OAuthCallbackRequest request)
        {
            try
            {
                var result = await _mediator.Send(
                    new AddGoogleCallback(request.Code, request.RedirectUri));
                return StatusCode(result.Status, result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error processing request.");
                return StatusCode(StatusCodes.Status500InternalServerError, new ResponseHttp
                {
                    Status = StatusCodes.Status500InternalServerError,
                    FailMessages = "An unexpected error occurred.",
                });
            }
        }

        /// <summary>
        /// Vérifier l'email via le token envoyé par email
        /// </summary>
        [HttpGet("verify-email")]
        [ProducesResponseType(typeof(ResponseHttp), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ResponseHttp), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ResponseHttp), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ResponseHttp>> VerifyEmail([FromQuery] string token)
        {
            try
            {
                var result = await _mediator.Send(new VerifyEmailCommand(token));
                return StatusCode(result.Status, result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error processing request.");
                return StatusCode(StatusCodes.Status500InternalServerError, new ResponseHttp
                {
                    Status = StatusCodes.Status500InternalServerError,
                    FailMessages = "An unexpected error occurred.",
                });
            }
        }

        /// <summary>
        /// Callback OAuth GitHub
        /// </summary>
        [HttpPost("github/callback")]
        [ProducesResponseType(typeof(ResponseHttp), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ResponseHttp), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ResponseHttp), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ResponseHttp>> GithubCallback(
            [FromBody] OAuthCallbackRequest request)
        {
            try
            {
                var result = await _mediator.Send(
                    new AddGithubCallback(request.Code, request.RedirectUri));
                return StatusCode(result.Status, result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error processing request.");
                return StatusCode(StatusCodes.Status500InternalServerError, new ResponseHttp
                {
                    Status = StatusCodes.Status500InternalServerError,
                    FailMessages = "An unexpected error occurred.",
                });
            }
        }
    }
}