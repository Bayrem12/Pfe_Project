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
                            FailMessages = "Member not found in this project",
                            Status = StatusCodes.Status400BadRequest,
                        };
                    }

                    if (member.Role == ProjectRole.Admin || member.Role == ProjectRole.Manager)
                    {
                        var projectMembers = await _projectRepository.GetProjectMembersAsync(request.ProjectId, cancellationToken);

                        // Count all remaining members who can manage the project (Admin OR Manager)
                        var remainingManagingCount = projectMembers.Count(m =>
                            (m.Role == ProjectRole.Admin || m.Role == ProjectRole.Manager) &&
                            m.UserId != request.UserId);

                        if (remainingManagingCount == 0)
                        {
                            return new ResponseHttp
                            {
                                FailMessages = "Cannot remove the last manager of the project. Assign another manager first.",
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
                        FailMessages = ex.Message,
                        Status = StatusCodes.Status400BadRequest,
                    };
                }
            }
        }
    }
}
