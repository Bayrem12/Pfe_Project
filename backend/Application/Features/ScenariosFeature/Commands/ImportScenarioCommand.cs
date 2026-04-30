using Application.Features.ScenariosFeature.DTOs;
using Application.Interfaces;
using Application.Setting;
using AutoMapper;
using Domain.Entities.Scenarios;
using Domain.Enums;
using Domain.Interfaces.Services;
using MediatR;
using Microsoft.AspNetCore.Http;

namespace Application.Features.ScenariosFeature.Commands
{
    public record ImportScenarioCommand(
        Guid FeatureId,
        string FeatureFileContent,
        Guid ImportedById
    ) : IRequest<ResponseHttp>;

    public class ImportScenarioCommandHandler : IRequestHandler<ImportScenarioCommand, ResponseHttp>
    {
        private readonly IScenarioRepository _scenarioRepository;
        private readonly IGherkinParserService _gherkinParser;
        private readonly IMapper _mapper;

        public ImportScenarioCommandHandler(
            IScenarioRepository scenarioRepository,
            IGherkinParserService gherkinParser,
            IMapper mapper)
        {
            _scenarioRepository = scenarioRepository;
            _gherkinParser = gherkinParser;
            _mapper = mapper;
        }

        public async Task<ResponseHttp> Handle(ImportScenarioCommand request, CancellationToken cancellationToken)
        {
            try
            {
                if (!_gherkinParser.ValidateSyntax(request.FeatureFileContent, out var errors))
                {
                    return new ResponseHttp
                    {
                        Fail_Messages = string.Join(", ", errors),
                        Status = StatusCodes.Status400BadRequest
                    };
                }

                var (featureName, parsedScenarios) = _gherkinParser.ParseFeatureContent(request.FeatureFileContent);

                if (!parsedScenarios.Any())
                {
                    return new ResponseHttp
                    {
                        Fail_Messages = "No scenarios found in the provided Gherkin content.",
                        Status = StatusCodes.Status400BadRequest
                    };
                }

                var importedScenarios = new List<ScenarioDto>();

                foreach (var parsed in parsedScenarios)
                {
                    var scenario = new Scenario
                    {
                        Id = Guid.NewGuid(),
                        FeatureId = request.FeatureId,
                        Title = parsed.Name,
                        Description = parsed.Description,
                        GherkinContent = BuildGherkinContent(featureName, parsed),
                        Status = ScenarioStatus.Draft,
                        CurrentVersion = 1,
                        CreatedDate = DateTime.UtcNow
                    };

                    int order = 0;
                    foreach (var step in parsed.Steps)
                    {
                        scenario.Steps.Add(new Step
                        {
                            Id = Guid.NewGuid(),
                            StepType = Enum.Parse<StepType>(step.Keyword, true),
                            Text = step.Text,
                            DisplayOrder = order++,
                            DataTable = step.DataTable
                        });
                    }

                    scenario.Versions.Add(new ScenarioVersion
                    {
                        Id = Guid.NewGuid(),
                        VersionNumber = 1,
                        GherkinContent = scenario.GherkinContent,
                        ChangeDescription = "Imported from .feature file",
                        CreatedDate = DateTime.UtcNow
                    });

                    scenario = await _scenarioRepository.Post(scenario);
                    importedScenarios.Add(_mapper.Map<ScenarioDto>(scenario));
                }

                await _scenarioRepository.SaveChange(cancellationToken);

                return new ResponseHttp
                {
                    Resultat = importedScenarios,
                    Status = StatusCodes.Status201Created
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

        private static string BuildGherkinContent(string featureName, ParsedScenario parsed)
        {
            var lines = new List<string>();
            if (!string.IsNullOrWhiteSpace(featureName))
                lines.Add($"Feature: {featureName}");

            lines.Add($"  Scenario: {parsed.Name}");
            foreach (var step in parsed.Steps)
            {
                lines.Add($"    {step.Keyword} {step.Text}");
                if (!string.IsNullOrWhiteSpace(step.DataTable))
                    lines.Add($"      {step.DataTable}");
            }

            return string.Join(Environment.NewLine, lines);
        }
    }
}
