using Application.Features.ScenariosFeature.DTOs;
using Application.Interfaces;
using Application.Setting;
using AutoMapper;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Application.Features.ScenariosFeature.Commands
{
    public record SaveQualityScoreCommand(
        Guid ScenarioId,
        int Score,
        string Label,
        Guid UserId
    ) : IRequest<ResponseHttp>
    {
        public class SaveQualityScoreCommandHandler : IRequestHandler<SaveQualityScoreCommand, ResponseHttp>
        {
            private readonly ITestTestAutoumatisationContext _context;
            private readonly IMapper _mapper;
            private readonly ILogger<SaveQualityScoreCommandHandler> _logger;

            public SaveQualityScoreCommandHandler(
                ITestTestAutoumatisationContext context,
                IMapper mapper,
                ILogger<SaveQualityScoreCommandHandler> logger)
            {
                _context = context;
                _mapper = mapper;
                _logger = logger;
            }

            public async Task<ResponseHttp> Handle(SaveQualityScoreCommand request, CancellationToken cancellationToken)
            {
                try
                {
                    var scenario = await _context.Scenarios
                        .FirstOrDefaultAsync(s => s.Id == request.ScenarioId && !s.IsDeleted, cancellationToken);

                    if (scenario == null)
                        return new ResponseHttp
                        {
                            Status = StatusCodes.Status404NotFound,
                            FailMessages = "Scenario not found."
                        };

                    scenario.QualityScore = Math.Clamp(request.Score, 0, 100);
                    scenario.QualityLabel = request.Label;
                    scenario.LastAnalyzedAt = DateTime.UtcNow;

                    await _context.SaveChangesAsync(cancellationToken);

                    return new ResponseHttp
                    {
                        Resultat = _mapper.Map<ScenarioDto>(scenario),
                        Status = StatusCodes.Status200OK
                    };
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error saving quality score for scenario {ScenarioId}", request.ScenarioId);
                    return new ResponseHttp
                    {
                        FailMessages = "An unexpected error occurred while saving the quality score.",
                        Status = StatusCodes.Status500InternalServerError
                    };
                }
            }
        }
    }
}
