using Application.Features.UserFeature.Dtos;
using Application.Interfaces.Identity;
using Application.Interfaces;          // ← ajouter
using Application.Setting;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Domain.Entities.Identity;

namespace Application.Features.AuthFeature.Commands
{
    public record AddGoogleCallback(string Code, string RedirectUri) : IRequest<ResponseHttp>
    {
        public class AddGoogleCallbackHandler : IRequestHandler<AddGoogleCallback, ResponseHttp>
        {
            private readonly IUserRepository _userRepository;
            private readonly IConfiguration _configuration;
            private readonly IGoogleAuthService _googleAuth;

            public AddGoogleCallbackHandler(
                IUserRepository userRepository,
                IConfiguration configuration,
                IGoogleAuthService googleAuth)
            {
                _userRepository = userRepository;
                _configuration = configuration;
                _googleAuth = googleAuth;
            }

            public async Task<ResponseHttp> Handle(
                AddGoogleCallback request,
                CancellationToken cancellationToken)
            {
                try
                {
                    // 1. Échanger le code contre le profil Google
                    var oauthUser = await _googleAuth.GetUserFromCodeAsync(
                        request.Code,
                        request.RedirectUri
                    );

                    if (oauthUser == null)
                        return new ResponseHttp
                        {
                            Status = StatusCodes.Status400BadRequest,
                            FailMessages = "Invalid Google token."
                        };

                    // 2. Trouver ou créer l'utilisateur
                    var user = await _userRepository.GetByEmailAsync(oauthUser.Email);

                    if (user == null)
                    {
                        user = new User
                        {
                            FirstName = oauthUser.FirstName,
                            LastName = oauthUser.LastName,
                            Email = oauthUser.Email,
                            PasswordHash = string.Empty, // pas de mot de passe OAuth
                            IsActive = true
                        };
                        await _userRepository.CreateWithDefaultRoleAsync(user);
                    }

                    // 3. Générer le JWT — même logique que AddUserLogin
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