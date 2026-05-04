using Application.Features.UserFeature.Dtos;
using Application.Interfaces.Identity;
using Application.Setting;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Application.Features.UserFeature.Commands
{
    /// <summary>
    /// Rafraîchit l'access token en échange d'un refresh token opaque valide.
    /// ✅ Critique 2 — Refresh token avec rotation (ancien token invalidé à chaque usage).
    /// </summary>
    public record AddUserRefresh(string RefreshToken) : IRequest<ResponseHttp>
    {
        public class AddUserRefreshHandler : IRequestHandler<AddUserRefresh, ResponseHttp>
        {
            private readonly IUserRepository _userRepository;
            private readonly IConfiguration _configuration;

            public AddUserRefreshHandler(IUserRepository userRepository, IConfiguration configuration)
            {
                _userRepository = userRepository;
                _configuration = configuration;
            }

            public async Task<ResponseHttp> Handle(AddUserRefresh request, CancellationToken cancellationToken)
            {
                if (string.IsNullOrWhiteSpace(request.RefreshToken))
                    return new ResponseHttp
                    {
                        FailMessages = "Le refresh token est requis.",
                        Status = StatusCodes.Status400BadRequest
                    };

                // Hash le token entrant et cherche l'utilisateur correspondant
                var hashedIncoming = Convert.ToHexString(
                    System.Security.Cryptography.SHA256.HashData(
                        Encoding.UTF8.GetBytes(request.RefreshToken)));

                var user = await _userRepository.GetByRefreshTokenAsync(hashedIncoming);

                if (user == null)
                    return new ResponseHttp
                    {
                        FailMessages = "Refresh token invalide ou expiré.",
                        Status = StatusCodes.Status401Unauthorized
                    };

                // Rotation — invalider l'ancien token immédiatement
                var newRefreshToken = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
                var newHashedRefresh = Convert.ToHexString(
                    System.Security.Cryptography.SHA256.HashData(
                        Encoding.UTF8.GetBytes(newRefreshToken)));

                var refreshExpiryDays = int.TryParse(
                    _configuration["Jwt:RefreshTokenExpirationDays"], out var days) ? days : 7;

                user.RefreshToken = newHashedRefresh;
                user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(refreshExpiryDays);
                await _userRepository.UpdateAsync(user);

                // Nouveau access token avec le rôle
                var roleName = user.UserRoles.FirstOrDefault()?.Role?.Name ?? "Viewer";
                var claims = new[]
                {
                    new Claim(ClaimTypes.Name, user.Email),
                    new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                    new Claim(ClaimTypes.Role, roleName)
                };

                var key = new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]!));
                var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

                var expirationMinutes = int.TryParse(
                    _configuration["Jwt:AccessTokenExpirationMinutes"], out var mins) ? mins : 60;

                var token = new JwtSecurityToken(
                    issuer: _configuration["Jwt:Issuer"],
                    audience: _configuration["Jwt:Audience"],
                    claims: claims,
                    expires: DateTime.UtcNow.AddMinutes(expirationMinutes),
                    signingCredentials: creds
                );

                return new ResponseHttp
                {
                    Status = StatusCodes.Status200OK,
                    Resultat = new
                    {
                        token = new JwtSecurityTokenHandler().WriteToken(token),
                        refreshToken = newRefreshToken,
                        user = UserDTO.FromEntity(user)
                    }
                };
            }
        }
    }
}

