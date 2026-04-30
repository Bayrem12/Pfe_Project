using Application.Features.ProjectFeature.Dtos;
using Application.Interfaces;
using Application.Setting;
using AutoMapper;
using Domain.Common;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.ProjectFeature.Queries
{
    public record GetAllProjectsQuery(Guid UserId, int? PageNumber, int? PageSize) : IRequest<ResponseHttp>
    {
        public class GetAllProjectsQueryHandler : IRequestHandler<GetAllProjectsQuery, ResponseHttp>
        {
            private readonly IProjectRepository _projectRepository;
            private readonly IMapper _mapper;
            private readonly ITestTestAutoumatisationContext _dbContext;

            public GetAllProjectsQueryHandler(IProjectRepository projectRepository, IMapper mapper, ITestTestAutoumatisationContext dbContext)
            {
                _projectRepository = projectRepository;
                _mapper = mapper;
                _dbContext = dbContext;
            }

            public async Task<ResponseHttp> Handle(GetAllProjectsQuery request, CancellationToken cancellationToken)
            {
                var projects = await _projectRepository.GetAllProjectsByUserIdAsync(
                    request.UserId, 
                    request.PageNumber, 
                    request.PageSize, 
                    cancellationToken);

                if (projects == null || projects.TotalCount == 0)
                {
                    return new ResponseHttp
                    {
                        Fail_Messages = "No projects found!",
                        Status = StatusCodes.Status400BadRequest,
                    };
                }

                var projectsToReturn = _mapper.Map<PagedList<ProjectDTO>>(projects);

                // Materialize Items to ensure modifications persist
                var items = projectsToReturn.Items.ToList();

                // Efficiently compute ModulesCount and ScenariosCount via SQL COUNT queries
                var projectIds = items.Select(p => p.Id).ToList();

                var moduleCounts = await _dbContext.Modules
                    .AsNoTracking()
                    .Where(m => projectIds.Contains(m.ProjectId) && !m.IsDeleted)
                    .GroupBy(m => m.ProjectId)
                    .Select(g => new { ProjectId = g.Key, Count = g.Count() })
                    .ToDictionaryAsync(x => x.ProjectId, x => x.Count, cancellationToken);

                var scenarioCounts = await _dbContext.Scenarios
                    .AsNoTracking()
                    .Where(s => !s.IsDeleted
                        && !s.Feature.IsDeleted
                        && !s.Feature.Module.IsDeleted
                        && projectIds.Contains(s.Feature.Module.ProjectId))
                    .GroupBy(s => s.Feature.Module.ProjectId)
                    .Select(g => new { ProjectId = g.Key, Count = g.Count() })
                    .ToDictionaryAsync(x => x.ProjectId, x => x.Count, cancellationToken);

                foreach (var dto in items)
                {
                    dto.ModulesCount = moduleCounts.GetValueOrDefault(dto.Id, 0);
                    dto.ScenariosCount = scenarioCounts.GetValueOrDefault(dto.Id, 0);
                }

                projectsToReturn.Items = items;

                return new ResponseHttp
                {
                    Status = StatusCodes.Status200OK,
                    Resultat = projectsToReturn
                };
            }
        }
    }
}
