using Application.Features.UserFeature.DTOs;
using Application.Interfaces.Identity;
using Application.Setting;
using MediatR;
using Microsoft.AspNetCore.Http;

namespace Application.Features.UserFeature.Commands
{
    public record UpdateUserRolesByIdCommand(
        Guid userId,
        List<string> roles
    ) : IRequest<ResponseHttp>
    {
        public class UpdateUserRolesCommandHandler : IRequestHandler<UpdateUserRolesByIdCommand, ResponseHttp>
        {
            private readonly IUserRepository _userRepository;

            public UpdateUserRolesCommandHandler(IUserRepository userRepository)
            {
                _userRepository = userRepository;
            }

            public async Task<ResponseHttp> Handle(UpdateUserRolesByIdCommand request, CancellationToken cancellationToken)
            {
                try
                {
                    var user = await _userRepository.GetUserByIdAsync(request.userId);
                    if (user == null)
                    {
                        return new ResponseHttp
                        {
                            FailMessages = "User with this Id not found.",
                            Status = StatusCodes.Status400BadRequest
                        };
                    }

                    await _userRepository.UpdateUserRolesAsync(request.userId, request.roles);

                    var updatedUser = await _userRepository.GetUserByIdAsync(request.userId);
                    var updatedRoles = updatedUser?.UserRoles
                        .Select(ur => ur.Role.Name)
                        .ToList() ?? new List<string>();

                    return new ResponseHttp
                    {
                        Resultat = updatedRoles,
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
