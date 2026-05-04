using Application.Interfaces;
using Application.Setting;
using MediatR;
using Microsoft.AspNetCore.Http;

namespace Application.Features.ProjectFeature.Commands
{
    public record DeleteProjectCommand(Guid ProjectId) : IRequest<ResponseHttp>
    {
        public class DeleteProjectCommandHandler : IRequestHandler<DeleteProjectCommand, ResponseHttp>
        {
            private readonly IProjectRepository _projectRepository;

            public DeleteProjectCommandHandler(IProjectRepository projectRepository)
            {
                _projectRepository = projectRepository;
            }

            public async Task<ResponseHttp> Handle(DeleteProjectCommand request, CancellationToken cancellationToken)
            {
                var project = await _projectRepository.GetById(request.ProjectId);

                if (project == null)
                {
                    return new ResponseHttp
                    {
                        FailMessages = "Project not found",
                        Status = StatusCodes.Status400BadRequest,
                    };
                }

                await _projectRepository.SoftDelete(request.ProjectId);
                await _projectRepository.SaveChange(cancellationToken);

                return new ResponseHttp
                {
                    Status = StatusCodes.Status200OK,
                };
            }
        }
    }
}
