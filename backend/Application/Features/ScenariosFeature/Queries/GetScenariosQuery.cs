using Application.Features.ScenariosFeature.DTOs;
using Application.Interfaces;
using Application.Setting;
using AutoMapper;
using Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.ScenariosFeature.Queries
{
    public record GetScenariosQuery(
        Guid? ProjectId,
        Guid? FeatureId,
        string? Search,
        ScenarioStatus? Status,
        int PageNumber,
        int PageSize
    ) : IRequest<ResponseHttp>
    {
        public class GetScenariosQueryHandler : IRequestHandler<GetScenariosQuery, ResponseHttp>
        {
            private readonly IScenarioRepository _scenarioRepository;
            private readonly IMapper _mapper;

            public GetScenariosQueryHandler(IScenarioRepository scenarioRepository, IMapper mapper)
            {
                _scenarioRepository = scenarioRepository;
                _mapper = mapper;
            }

            public async Task<ResponseHttp> Handle(GetScenariosQuery request, CancellationToken cancellationToken)
            {
                try
                {
                    var query = _scenarioRepository.GetQueryable()
                        .Include(s => s.Feature)
                            .ThenInclude(f => f.Module)
                        .Include(s => s.Steps)
                        .Include(s => s.ScenarioTags)
                            .ThenInclude(st => st.Tag)
                        .Include(s => s.TestResults)
                        .Where(s => !s.IsDeleted);

                    // ✅ Filter by Project
                    if (request.ProjectId.HasValue)
                    {
                        query = query.Where(s => s.Feature.Module.ProjectId == request.ProjectId.Value);
                    }

                    // ✅ Filter by Feature
                    if (request.FeatureId.HasValue)
                    {
                        query = query.Where(s => s.FeatureId == request.FeatureId.Value);
                    }

                    // ✅ Search
                    if (!string.IsNullOrWhiteSpace(request.Search))
                    {
                        var searchLower = request.Search.ToLower();
                        query = query.Where(s =>
                            s.Title.ToLower().Contains(searchLower) ||
                            s.Description.ToLower().Contains(searchLower)
                        );
                    }

                    // ✅ Status
                    if (request.Status.HasValue)
                    {
                        query = query.Where(s => s.Status == request.Status.Value);
                    }

                    var scenarios = await query
                        .OrderByDescending(s => s.ModifiedDate ?? s.CreatedDate)
                        .Skip((request.PageNumber - 1) * request.PageSize)
                        .Take(request.PageSize)
                        .ToListAsync(cancellationToken);

                    var scenariosDto = _mapper.Map<List<ScenarioDto>>(scenarios);

                    return new ResponseHttp
                    {
                        Resultat = scenariosDto,
                        Status = StatusCodes.Status200OK
                    };
                }
                catch (Exception ex)
                {
                    var innerMessage = ex.InnerException?.Message ?? ex.Message;
                    return new ResponseHttp
                    {
                        Fail_Messages = innerMessage,
                        Status = StatusCodes.Status400BadRequest
                    };
                }
            }
        }
    }
}