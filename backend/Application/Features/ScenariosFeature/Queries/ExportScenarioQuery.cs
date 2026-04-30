using Application.Interfaces;
using Application.Setting;
using MediatR;
using Microsoft.AspNetCore.Http;

namespace Application.Features.ScenariosFeature.Queries
{
    public record ExportScenarioQuery(Guid Id, Guid CurrentUserId) : IRequest<ResponseHttp>
    {
        public class ExportScenarioQueryHandler : IRequestHandler<ExportScenarioQuery, ResponseHttp>
        {
            private readonly IScenarioRepository _scenarioRepository;

            public ExportScenarioQueryHandler(IScenarioRepository scenarioRepository)
            {
                _scenarioRepository = scenarioRepository;
            }

            public async Task<ResponseHttp> Handle(ExportScenarioQuery request, CancellationToken cancellationToken)
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

                    // 👉 (optionnel) check sécurité ici

                    return new ResponseHttp
                    {
                        Resultat = new
                        {
                            content = scenario.GherkinContent
                        },
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