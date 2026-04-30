using Application.Features.UserFeature.Dtos;
using Application.Interfaces.Identity;
using Application.Setting;
using MediatR;
using Microsoft.AspNetCore.Http;

namespace Application.Features.UserFeature.Commands
{
    /// <summary>
    /// Commande pour changer le mot de passe d'un utilisateur (POST /api/Auth/change-password).
    /// </summary>
    public record AddUserChangePassword(string Email, string CurrentPassword, string NewPassword) : IRequest<ResponseHttp>
    {
        public class AddUserChangePasswordHandler : IRequestHandler<AddUserChangePassword, ResponseHttp>
        {
            private readonly IUserRepository _userRepository;

            public AddUserChangePasswordHandler(IUserRepository userRepository)
            {
                _userRepository = userRepository;
            }

            public async Task<ResponseHttp> Handle(AddUserChangePassword request, CancellationToken cancellationToken)
            {
                try
                {
                    var user = await _userRepository.GetByEmailAsync(request.Email);

                    if (user == null)
                        return new ResponseHttp
                        {
                            Fail_Messages = "Utilisateur introuvable.",
                            Status = StatusCodes.Status404NotFound
                        };

                    // ✅ BCrypt.Verify — remplace SHA256
                    if (!BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.PasswordHash))
                        return new ResponseHttp
                        {
                            Fail_Messages = "Mot de passe courant invalide.",
                            Status = StatusCodes.Status401Unauthorized
                        };

                    // ✅ BCrypt.HashPassword — remplace SHA256
                    user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
                    await _userRepository.UpdateAsync(user);

                    return new ResponseHttp
                    {
                        Resultat = UserDTO.FromEntity(user),
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