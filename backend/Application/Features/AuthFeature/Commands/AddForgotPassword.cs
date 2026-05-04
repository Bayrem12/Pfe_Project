using Application.Interfaces;
using Application.Interfaces.Identity;
using Application.Setting;
using Domain.Common;
using MediatR;
using Microsoft.AspNetCore.Http;
using System.Security.Cryptography;

namespace Application.Features.AuthFeature.Commands
{
    public record AddForgotPassword(string Email) : IRequest<ResponseHttp>
    {
        public class AddForgotPasswordHandler : IRequestHandler<AddForgotPassword, ResponseHttp>
        {
            private readonly IUserRepository _userRepository;
            private readonly IEmailService _emailService;

            public AddForgotPasswordHandler(
                IUserRepository userRepository,
                IEmailService emailService)
            {
                _userRepository = userRepository;
                _emailService = emailService;
            }

            public async Task<ResponseHttp> Handle(AddForgotPassword request, CancellationToken cancellationToken)
            {
                var user = await _userRepository.GetByEmailAsync(request.Email);

                if (user == null || !user.IsActive)
                    return new ResponseHttp
                    {
                        Status = StatusCodes.Status404NotFound,
                        FailMessages = "No account found with this email address."
                    };

                // Générer le token sécurisé
                var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
                user.PasswordResetToken = token;
                user.PasswordResetTokenExpiry = DateTime.UtcNow.AddHours(1);
                await _userRepository.UpdateAsync(user);

                await _emailService.SendPasswordResetEmailAsync(user.Email, token);

                return new ResponseHttp
                {
                    Status = StatusCodes.Status200OK,
                    Resultat = "If this email exists, a reset link has been sent."
                };
            }
        }
    }
}
