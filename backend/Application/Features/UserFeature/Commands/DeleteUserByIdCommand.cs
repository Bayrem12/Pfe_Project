using Application.Interfaces.Identity;
using Application.Setting;
using MediatR;
using Microsoft.AspNetCore.Http;

namespace Application.Features.UserFeature.Commands
{
    public record DeleteUserByIdCommand(Guid UserId) : IRequest<ResponseHttp>
    {
        public class DeleteUserByIdCommandHandler : IRequestHandler<DeleteUserByIdCommand, ResponseHttp>
        {
            private readonly IUserRepository _userRepository;

            public DeleteUserByIdCommandHandler(IUserRepository userRepository)
            {
                _userRepository = userRepository;
            }

            public async Task<ResponseHttp> Handle(DeleteUserByIdCommand request, CancellationToken cancellationToken)
            {
                try
                {
                    var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken);

                    if (user == null)
                    {
                        return new ResponseHttp
                        {
                            Fail_Messages = "User with this Id not found.",
                            Status = StatusCodes.Status404NotFound
                        };
                    }

                    user.IsDeleted = true;
                    user.DeletedDate = DateTimeOffset.UtcNow;

                    await _userRepository.UpdateAsync(user);

                    return new ResponseHttp
                    {
                        Status = StatusCodes.Status204NoContent
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
