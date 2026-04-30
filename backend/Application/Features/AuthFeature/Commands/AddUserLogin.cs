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
    public record AddUserLogin(string Email, string Password) : IRequest<ResponseHttp>
    {
        public class AddUserLoginHandler : IRequestHandler<AddUserLogin, ResponseHttp>
        {
            private readonly IUserRepository _userRepository;
            private readonly IConfiguration _configuration;

            public AddUserLoginHandler(IUserRepository userRepository, IConfiguration configuration)
            {
                _userRepository = userRepository;
                _configuration = configuration;
            }

            public async Task<ResponseHttp> Handle(AddUserLogin request, CancellationToken cancellationToken)
            {
                try
                {
                    var user = await _userRepository.GetByEmailAsync(request.Email);

                    if (user == null)
                        return new ResponseHttp
                        {
                            Fail_Messages = "Email ou mot de passe invalide.",
                            Status = StatusCodes.Status401Unauthorized
                        };

                    if (!user.IsActive)
                        return new ResponseHttp
                        {
                            Fail_Messages = "Please verify your email address before logging in. Check your inbox for the confirmation link.",
                            Status = StatusCodes.Status403Forbidden
                        };

                    // ✅ BCrypt.Verify — remplace SHA256
                    if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
                        return new ResponseHttp
                        {
                            Fail_Messages = "Email ou mot de passe invalide.",
                            Status = StatusCodes.Status401Unauthorized
                        };

                    var claims = new[]
                    {
                        new Claim(ClaimTypes.Name, user.Email),
                        new Claim(ClaimTypes.NameIdentifier, user.Id.ToString())
                    };

                    var key = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]!));
                    var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

                    // ✅ Fix GetValue → int.TryParse sur _configuration["clé"]
                    // GetValue<T> est une extension de Microsoft.Extensions.Configuration.Binder
                    // non disponible dans classlib → on utilise l'indexeur de base
                    var expirationMinutes = int.TryParse(
                        _configuration["Jwt:AccessTokenExpirationMinutes"], out var mins) ? mins : 60;

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
                        Resultat = new
                        {
                            user = UserDTO.FromEntity(user),
                            token = tokenString
                        },
                        Status = StatusCodes.Status200OK
                    };
                }
                catch (Exception ex)
                {
                    return new ResponseHttp
                    {
                        Fail_Messages = ex.Message,
                        Status = StatusCodes.Status400BadRequest
                    };
                }
            }
        }
    }
}