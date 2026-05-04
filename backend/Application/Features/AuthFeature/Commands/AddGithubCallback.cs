using Application.Features.UserFeature.Dtos;
using Application.Interfaces;
using Application.Interfaces.Identity;
using Application.Setting;
using Domain.Entities.Identity;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Application.Features.AuthFeature.Commands
{
    public record AddGithubCallback(string Code, string RedirectUri) : IRequest<ResponseHttp>
    {
        public class AddGithubCallbackHandler : IRequestHandler<AddGithubCallback, ResponseHttp>
        {
            private readonly IUserRepository _userRepository;
            private readonly IConfiguration _configuration;
            private readonly IGithubAuthService _githubAuth;

            public AddGithubCallbackHandler(
                IUserRepository userRepository,
                IConfiguration configuration,
                IGithubAuthService githubAuth)
            {
                _userRepository = userRepository;
                _configuration = configuration;
                _githubAuth = githubAuth;
            }

            public async Task<ResponseHttp> Handle(
                AddGithubCallback request,
                CancellationToken cancellationToken)
            {
                try
                {
                    var oauthUser = await _githubAuth.GetUserFromCodeAsync(
                        request.Code,
                        request.RedirectUri
                    );

                    if (oauthUser == null || string.IsNullOrEmpty(oauthUser.Email))
                        return new ResponseHttp
                        {
                            Status = StatusCodes.Status400BadRequest,
                            FailMessages = "Invalid GitHub token or email not accessible."
                        };

                    var user = await _userRepository.GetByEmailAsync(oauthUser.Email);

                    if (user == null)
                    {
                        user = new User
                        {
                            FirstName = oauthUser.FirstName,
                            LastName = oauthUser.LastName,
                            Email = oauthUser.Email,
                            PasswordHash = string.Empty,
                            IsActive = true
                        };
                        await _userRepository.CreateWithDefaultRoleAsync(user);
                    }

                    // ✅ Critique 1 — Inclure le rôle dans le JWT
                    var roleName = user.UserRoles.FirstOrDefault()?.Role?.Name ?? "Viewer";
                    var claims = new[]
                    {
                        new Claim(ClaimTypes.Name,           user.Email),
                        new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                        new Claim(ClaimTypes.Role,           roleName)
                    };

                    var key = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]!));
                    var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

                    var expirationMinutes = int.TryParse(
                        _configuration["Jwt:AccessTokenExpirationMinutes"],
                        out var mins) ? mins : 60;

                    var token = new JwtSecurityToken(
                        issuer: _configuration["Jwt:Issuer"],
                        audience: _configuration["Jwt:Audience"],
                        claims: claims,
                        expires: DateTime.UtcNow.AddMinutes(expirationMinutes),
                        signingCredentials: creds
                    );

                    var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

                    return new ResponseHttp
                    {
                        Status = StatusCodes.Status200OK,
                        Resultat = new
                        {
                            token = tokenString,
                            user = UserDTO.FromEntity(user)
                        }
                    };
                }
                catch (Exception ex)
                {
                    return new ResponseHttp
                    {
                        Status = StatusCodes.Status500InternalServerError,
                        FailMessages = ex.Message
                    };
                }
            }
        }
    }
}