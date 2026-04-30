using Application.Interfaces.Identity;
using Application.Setting;
using MediatR;
using Microsoft.AspNetCore.Http;


namespace Application.Features.AuthFeature.Commands
{
    public record AddResetPassword(
        string Token,
        string NewPassword,
        string ConfirmPassword) : IRequest<ResponseHttp>
    {
        public class AddResetPasswordHandler : IRequestHandler<AddResetPassword, ResponseHttp>
        {
            private readonly IUserRepository _userRepository;

            public AddResetPasswordHandler(IUserRepository userRepository)
            {
                _userRepository = userRepository;
            }

            public async Task<ResponseHttp> Handle(AddResetPassword request, CancellationToken cancellationToken)
            {
                if (request.NewPassword != request.ConfirmPassword)
                    return new ResponseHttp
                    {
                        Status = StatusCodes.Status400BadRequest,
                        Fail_Messages = "Les mots de passe ne correspondent pas."
                    };

                var users = await _userRepository.GetAllUsersAsync();
                var user = users.FirstOrDefault(u =>
                    u.PasswordResetToken == request.Token &&
                    u.PasswordResetTokenExpiry > DateTime.UtcNow);

                if (user == null)
                    return new ResponseHttp
                    {
                        Status = StatusCodes.Status400BadRequest,
                        Fail_Messages = "Token invalide ou expiré."
                    };

                // Fix : utiliser BCrypt avec salt automatique au lieu de SHA256
                user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
                user.PasswordResetToken = null;
                user.PasswordResetTokenExpiry = null;

                await _userRepository.UpdateAsync(user);

                return new ResponseHttp
                {
                    Status = StatusCodes.Status200OK,
                    Resultat = "Mot de passe réinitialisé avec succès."
                };
            }
        }
    }
}