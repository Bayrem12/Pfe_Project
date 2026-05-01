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
    public record AddProjectCommand(
        string Name,
        string Description,
        string? Url,
        Guid UserId,
        bool IsActive = true)
        : IRequest<ResponseHttp>
    {
        public class AddProjectCommandHandler : IRequestHandler<AddProjectCommand, ResponseHttp>
        {
            private readonly IProjectRepository _projectRepository;
            private readonly IMapper _mapper;

            public AddProjectCommandHandler(IProjectRepository projectRepository, IMapper mapper)
            {
                _projectRepository = projectRepository;
                _mapper = mapper;
            }

            public async Task<ResponseHttp> Handle(AddProjectCommand request, CancellationToken cancellationToken)
            {
                try
                {
                    // Unicité du nom
                    if (await _projectRepository.ExistsWithNameAsync(request.Name, null, cancellationToken))
                        return new ResponseHttp
                        {
                            Fail_Messages = "Ce projet existe déjà, veuillez choisir un autre nom.",
                            Status = StatusCodes.Status400BadRequest
                        };

                    // Unicité de l'URL
                    if (!string.IsNullOrWhiteSpace(request.Url) &&
                        await _projectRepository.ExistsWithUrlAsync(request.Url, null, cancellationToken))
                        return new ResponseHttp
                        {
                            Fail_Messages = "Cette URL est déjà utilisée par un autre projet.",
                            Status = StatusCodes.Status400BadRequest
                        };

                    var project = new Project
                    {
                        Id = Guid.NewGuid(),
                        Name = request.Name,
                        Description = request.Description,
                        Url = request.Url,
                        IsActive = request.IsActive,
                        UserId = request.UserId,
                        CreatedById = request.UserId.ToString(),
                        CreatedDate = DateTime.UtcNow
                    };

                    project = await _projectRepository.Post(project);
                    await _projectRepository.SaveChange(cancellationToken);

                    // Add creator as Owner member
                    var ownerMember = new ProjectMember
                    {
                        Id = Guid.NewGuid(),
                        ProjectId = project.Id,
                        UserId = request.UserId,
                        Role = ProjectRole.Owner,
                        JoinedAt = DateTime.UtcNow,
                        CreatedDate = DateTime.UtcNow
                    };
                    await _projectRepository.AddMemberAsync(ownerMember, cancellationToken);
                    await _projectRepository.SaveChange(cancellationToken);

                    return new ResponseHttp()
                    {
                        Resultat = _mapper.Map<ProjectDTO>(project),
                        Status = StatusCodes.Status201Created
                    };
                }
                catch (Exception ex)
                {
                    var innerMessage = ex.InnerException?.Message ?? ex.Message;
                    return new ResponseHttp
                    {
                        Fail_Messages = innerMessage,
                        Status = StatusCodes.Status400BadRequest,
                    };
                }
            }
        }
    }
}
