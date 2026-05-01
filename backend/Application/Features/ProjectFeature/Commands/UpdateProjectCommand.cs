using Application.Features.ProjectFeature.Dtos;
using Application.Interfaces;
using Application.Setting;
using AutoMapper;
using MediatR;
using Microsoft.AspNetCore.Http;

namespace Application.Features.ProjectFeature.Commands
{
    public record UpdateProjectCommand(
        Guid ProjectId,
        string Name,
        string Description,
        bool IsActive)
        : IRequest<ResponseHttp>
    {
        public class UpdateProjectCommandHandler : IRequestHandler<UpdateProjectCommand, ResponseHttp>
        {
            private readonly IProjectRepository _projectRepository;
            private readonly IMapper _mapper;

            public UpdateProjectCommandHandler(IProjectRepository projectRepository, IMapper mapper)
            {
                _projectRepository = projectRepository;
                _mapper = mapper;
            }

            public async Task<ResponseHttp> Handle(UpdateProjectCommand request, CancellationToken cancellationToken)
            {
                var project = await _projectRepository.GetById(request.ProjectId);

                if (project == null)
                {
                    return new ResponseHttp
                    {
                        FailMessages = "Project with this Id not found.",
                        Status = StatusCodes.Status400BadRequest,
                    };
                }

                project.Name = request.Name;
                project.Description = request.Description;
                project.IsActive = request.IsActive;

                await _projectRepository.Update(project);
                await _projectRepository.SaveChange(cancellationToken);

                return new ResponseHttp
                {
                    Resultat = _mapper.Map<ProjectDTO>(project),
                    Status = StatusCodes.Status200OK,
                };
            }
        }
    }
}
