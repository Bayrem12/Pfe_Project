using Application.Features.ProjectFeature.Dtos;
using Application.Interfaces;
using Application.Setting;
using AutoMapper;
using Domain.Entities.ProjectManagement;
using Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Http;

namespace Application.Features.ProjectFeature.Commands
{
    public record AddProjectMemberCommand(
        Guid ProjectId,
        Guid UserId,
        string Role)
        : IRequest<ResponseHttp>
    {
        public class AddProjectMemberCommandHandler : IRequestHandler<AddProjectMemberCommand, ResponseHttp>
        {
            private readonly IProjectRepository _projectRepository;
            private readonly IMapper _mapper;

            public AddProjectMemberCommandHandler(IProjectRepository projectRepository, IMapper mapper)
            {
                _projectRepository = projectRepository;
                _mapper = mapper;
            }

            public async Task<ResponseHttp> Handle(AddProjectMemberCommand request, CancellationToken cancellationToken)
            {
                try
                {
                    // Check if project exists
                    var project = await _projectRepository.GetById(request.ProjectId);
                    if (project == null)
                    {
                        return new ResponseHttp
                        {
                            Fail_Messages = "Project not found",
                            Status = StatusCodes.Status400BadRequest,
                        };
                    }

                    // Check if member already exists
                    var existingMember = await _projectRepository.GetProjectMemberAsync(request.ProjectId, request.UserId, cancellationToken);
                    if (existingMember != null)
                    {
                        return new ResponseHttp
                        {
                            Fail_Messages = "User is already a member of this project",
                            Status = StatusCodes.Status400BadRequest,
                        };
                    }

                    // Parse role
                    if (!Enum.TryParse<ProjectRole>(request.Role, true, out var role))
                    {
                        role = ProjectRole.Tester;
                    }

                    var member = new ProjectMember
                    {
                        ProjectId = request.ProjectId,
                        UserId = request.UserId,
                        Role = role,
                        JoinedAt = DateTime.UtcNow
                    };

                    await _projectRepository.AddMemberAsync(member, cancellationToken);
                    await _projectRepository.SaveChange(cancellationToken);

                    return new ResponseHttp()
                    {
                        Resultat = _mapper.Map<ProjectMemberDTO>(member),
                        Status = StatusCodes.Status201Created
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
