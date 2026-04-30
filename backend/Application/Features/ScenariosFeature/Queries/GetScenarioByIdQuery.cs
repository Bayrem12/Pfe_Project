using Application.Features.ScenariosFeature.DTOs;
using Application.Interfaces;
using Application.Setting;
using AutoMapper;
using MediatR;
using Microsoft.AspNetCore.Http;

namespace Application.Features.ScenariosFeature.Queries
{
    public record GetScenarioByIdQuery(Guid Id, Guid CurrentUserId) : IRequest<ResponseHttp>
    {
        public class GetScenarioByIdQueryHandler : IRequestHandler<GetScenarioByIdQuery, ResponseHttp>
        {
            private readonly IScenarioRepository _scenarioRepository;
            private readonly IMapper _mapper;

            public GetScenarioByIdQueryHandler(IScenarioRepository scenarioRepository, IMapper mapper)
            {
                _scenarioRepository = scenarioRepository;
                _mapper = mapper;
            }

            public async Task<ResponseHttp> Handle(GetScenarioByIdQuery request, CancellationToken cancellationToken)
            {
                try
                {
                    var scenario = await _scenarioRepository.GetFullAsync(request.Id, cancellationToken);

                    if (scenario == null)
                    {
                        return new ResponseHttp
                        {
                            Fail_Messages = "Scenario not found",
                            Status = StatusCodes.Status404NotFound
                        };
                    }

                    // 👉 (optionnel) ici tu peux ajouter check membership

                    var scenarioDto = _mapper.Map<ScenarioDetailDto>(scenario);

                    return new ResponseHttp
                    {
                        Resultat = scenarioDto,
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