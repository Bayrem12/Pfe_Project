using Application.Features.ProjectFeature.Dtos;
using Application.Interfaces;
using Application.Setting;
using AutoMapper;
using MediatR;
using Microsoft.AspNetCore.Http;

namespace Application.Features.ProjectFeature.Queries
{
    public record GetProjectMembersQuery(Guid ProjectId) : IRequest<ResponseHttp>
    {
        public class GetProjectMembersQueryHandler : IRequestHandler<GetProjectMembersQuery, ResponseHttp>
        {
            private readonly IProjectRepository _projectRepository;
            private readonly IMapper _mapper;

            public GetProjectMembersQueryHandler(IProjectRepository projectRepository, IMapper mapper)
            {
                _projectRepository = projectRepository;
                _mapper = mapper;
            }

            public async Task<ResponseHttp> Handle(GetProjectMembersQuery request, CancellationToken cancellationToken)
            {
                try
                {
                    // Check if project exists
                    var project = await _projectRepository.GetById(request.ProjectId);
                    if (project == null)
                    {
                        return new ResponseHttp()
                        {
                            Status = StatusCodes.Status404NotFound,
                            Fail_Messages = "Project not found!"
                        };
                    }

                    var members = await _projectRepository.GetProjectMembersAsync(request.ProjectId, cancellationToken);

                    return new ResponseHttp()
                    {
                        Resultat = _mapper.Map<List<ProjectMemberDTO>>(members),
                        Status = StatusCodes.Status200OK
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
