using Application.Interfaces;
using Application.Setting;
using Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Http;

namespace Application.Features.ProjectFeature.Commands
{
    public record RemoveProjectMemberCommand(
        Guid ProjectId,
        Guid UserId)
        : IRequest<ResponseHttp>
    {
        public class RemoveProjectMemberCommandHandler : IRequestHandler<RemoveProjectMemberCommand, ResponseHttp>
        {
            private readonly IProjectRepository _projectRepository;

            public RemoveProjectMemberCommandHandler(IProjectRepository projectRepository)
            {
                _projectRepository = projectRepository;
            }

            public async Task<ResponseHttp> Handle(RemoveProjectMemberCommand request, CancellationToken cancellationToken)
            {
                try
                {
                    var member = await _projectRepository.GetProjectMemberAsync(request.ProjectId, request.UserId, cancellationToken);

                    if (member == null)
                    {
                        return new ResponseHttp
                        {
                            Fail_Messages = "Member not found in this project",
                            Status = StatusCodes.Status400BadRequest,
                        };
                    }

                    if (member.Role == ProjectRole.Owner)
                    {
                        var projectMembers = await _projectRepository.GetProjectMembersAsync(request.ProjectId, cancellationToken);

                        var remainingOwnersCount = projectMembers.Count(m =>
                            m.Role == ProjectRole.Owner &&
                            m.UserId != request.UserId);

                        if (remainingOwnersCount == 0)
                        {
                            return new ResponseHttp
                            {
                                Fail_Messages = "Cannot remove the last owner of the project",
                                Status = StatusCodes.Status400BadRequest,
                            };
                        }
                    }

                    await _projectRepository.RemoveMemberAsync(request.ProjectId, request.UserId, cancellationToken);
                    await _projectRepository.SaveChange(cancellationToken);

                    return new ResponseHttp
                    {
                        Status = StatusCodes.Status204NoContent,
                    };
                }
                catch (Exception ex)
                {
                    return new ResponseHttp
                    {
                        Fail_Messages = ex.Message,
                        Status = StatusCodes.Status400BadRequest,
                    };
                }
            }
        }
    }
}
