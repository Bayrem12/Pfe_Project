using Application.Interfaces;
using Application.Interfaces.Identity;
using Application.Setting;
using MediatR;
using Microsoft.AspNetCore.Http;

namespace Application.Features.AuthFeature.Commands
{
    public record VerifyEmailCommand(string Token) : IRequest<ResponseHttp>
    {
        public class VerifyEmailCommandHandler : IRequestHandler<VerifyEmailCommand, ResponseHttp>
        {
            private readonly IUserRepository _userRepository;

            public VerifyEmailCommandHandler(IUserRepository userRepository)
            {
                _userRepository = userRepository;
            }

            public async Task<ResponseHttp> Handle(VerifyEmailCommand request, CancellationToken cancellationToken)
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(request.Token))
                        return new ResponseHttp
                        {
                            FailMessages = "Verification token is required.",
                            Status = StatusCodes.Status400BadRequest
                        };

                    var user = await _userRepository.GetByVerificationTokenAsync(request.Token);

                    if (user is null)
                        return new ResponseHttp
                        {
                            FailMessages = "Invalid verification link.",
                            Status = StatusCodes.Status400BadRequest
                        };

                    if (user.EmailVerificationTokenExpiry < DateTime.UtcNow)
                        return new ResponseHttp
                        {
                            FailMessages = "Verification link has expired. Please register again.",
                            Status = StatusCodes.Status400BadRequest
                        };

                    user.IsActive = true;
                    user.EmailVerifiedAt = DateTime.UtcNow;
                    user.EmailVerificationToken = null;
                    user.EmailVerificationTokenExpiry = null;

                    await _userRepository.UpdateAsync(user);

                    return new ResponseHttp
                    {
                        Resultat = "Email verified successfully. You can now log in.",
                        Status = StatusCodes.Status200OK
                    };
                }
                catch (Exception ex)
                {
                    return new ResponseHttp
                    {
                        FailMessages = ex.Message,
                        Status = StatusCodes.Status500InternalServerError
                    };
                }
            }
        }
    }
}
