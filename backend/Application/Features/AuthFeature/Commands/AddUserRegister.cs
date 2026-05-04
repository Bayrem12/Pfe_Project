using Application.Interfaces;
using Application.Interfaces.Identity;
using Application.Setting;
using Domain.Entities.Identity;
using MediatR;
using Microsoft.AspNetCore.Http;

namespace Application.Features.UserFeature.Commands
{
    public record AddUserRegister(
        string FirstName,
        string LastName,
        string Email,
        string Password) : IRequest<ResponseHttp>
    {
        public class AddUserRegisterHandler : IRequestHandler<AddUserRegister, ResponseHttp>
        {
            private readonly IUserRepository _userRepository;
            private readonly IEmailService _emailService;

            public AddUserRegisterHandler(IUserRepository userRepository, IEmailService emailService)
            {
                _userRepository = userRepository;
                _emailService = emailService;
            }

            public async Task<ResponseHttp> Handle(AddUserRegister request, CancellationToken cancellationToken)
            {
                try
                {
                    if (request is null)
                        return new ResponseHttp
                        {
                            FailMessages = "Requête invalide.",
                            Status = StatusCodes.Status400BadRequest
                        };

                    if (string.IsNullOrWhiteSpace(request.Email))
                        return new ResponseHttp
                        {
                            FailMessages = "L'adresse e-mail est requise.",
                            Status = StatusCodes.Status400BadRequest
                        };

                    if (string.IsNullOrWhiteSpace(request.Password))
                        return new ResponseHttp
                        {
                            FailMessages = "Le mot de passe est requis.",
                            Status = StatusCodes.Status400BadRequest
                        };

                    if (await _userRepository.ExistsAsync(request.Email))
                        return new ResponseHttp
                        {
                            FailMessages = "Un utilisateur avec cet email existe déjà.",
                            Status = StatusCodes.Status400BadRequest
                        };

                    var verificationToken = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");

                    var user = new User
                    {
                        Id = Guid.NewGuid(),
                        FirstName = request.FirstName,
                        LastName = request.LastName,
                        Email = request.Email,
                        PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
                        IsActive = false,
                        EmailVerificationToken = verificationToken,
                        EmailVerificationTokenExpiry = DateTime.UtcNow.AddHours(24)
                    };

                    await _userRepository.CreateWithDefaultRoleAsync(user);

                    // Fire and forget — don't block registration if email fails
                    _ = Task.Run(async () =>
                    {
                        try { await _emailService.SendVerificationEmailAsync(request.Email, verificationToken); }
                        catch { /* Log silently in production */ }
                    }, CancellationToken.None);

                    return new ResponseHttp
                    {
                        Resultat = "Registration successful! Please check your email to verify your account.",
                        Status = StatusCodes.Status200OK
                    };
                }
                catch (Exception ex)
                {
                    return new ResponseHttp
                    {
                        FailMessages = ex.Message,
                        Status = StatusCodes.Status400BadRequest
                    };
                }
            }
        }
    }
}