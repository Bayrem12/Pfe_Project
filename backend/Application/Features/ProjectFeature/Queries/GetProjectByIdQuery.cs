using Application.Features.ProjectFeature.Dtos;
using Application.Interfaces;
using Application.Setting;
using AutoMapper;
using MediatR;
using Microsoft.AspNetCore.Http;

namespace Application.Features.ProjectFeature.Queries
{
    public record GetProjectByIdQuery(Guid ProjectId) : IRequest<ResponseHttp>
    {
        public class GetProjectByIdQueryHandler : IRequestHandler<GetProjectByIdQuery, ResponseHttp>
        {
            private readonly IProjectRepository _projectRepository;
            private readonly IMapper _mapper;

            public GetProjectByIdQueryHandler(IProjectRepository projectRepository, IMapper mapper)
            {
                _projectRepository = projectRepository;
                _mapper = mapper;
            }

            public async Task<ResponseHttp> Handle(GetProjectByIdQuery request, CancellationToken cancellationToken)
            {
                try
                {
                    var project = await _projectRepository.GetProjectWithMembersAsync(request.ProjectId, cancellationToken);

                    if (project == null)
                    {
                        return new ResponseHttp()
                        {
                            Status = StatusCodes.Status404NotFound,
                            FailMessages = "Project not found!"
                        };
                    }

                    return new ResponseHttp()
                    {
                        Resultat = _mapper.Map<ProjectDTO>(project),
                        Status = StatusCodes.Status200OK
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
