using Application.Features.ScenariosFeature.DTOs;
using Application.Interfaces;
using Application.Setting;
using AutoMapper;
using Domain.Entities.Scenarios;
using Domain.Enums;
using Domain.Interfaces.Services;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.ScenariosFeature.Commands
{
    public record CreateScenarioCommand(
        Guid FeatureId,
        string Title,
        string Description,
        string GherkinContent,
        Guid CreatedById,
        ScenarioStatus Status = ScenarioStatus.Draft,
        List<string>? Tags = null
    ) : IRequest<ResponseHttp>
    {
        public class Handler : IRequestHandler<CreateScenarioCommand, ResponseHttp>
        {
            private readonly IScenarioRepository _scenarioRepository;
            private readonly IFeatureRepository _featureRepository;
            private readonly IProjectRepository _projectRepository;
            private readonly IGherkinParserService _gherkinParser;
            private readonly IMapper _mapper;

            public Handler(
                IScenarioRepository scenarioRepository,
                IFeatureRepository featureRepository,
                IProjectRepository projectRepository,
                IGherkinParserService gherkinParser,
                IMapper mapper)
            {
                _scenarioRepository = scenarioRepository;
                _featureRepository = featureRepository;
                _projectRepository = projectRepository;
                _gherkinParser = gherkinParser;
                _mapper = mapper;
            }

            public async Task<ResponseHttp> Handle(CreateScenarioCommand request, CancellationToken ct)
            {
                // 🔒 Get feature with Module + Project
                var feature = await _featureRepository.GetById(request.FeatureId);

                if (feature == null)
                {
                    return new ResponseHttp
                    {
                        FailMessages = "Feature not found",
                        Status = StatusCodes.Status404NotFound
                    };
                }

                var projectId = feature.Module.ProjectId;

                // 🔒 Check membership
                var project = await _projectRepository.GetProjectWithMembersAsync(projectId, ct);
                var isMember = project != null &&
                    (project.UserId == request.CreatedById ||
                     project.Members.Any(m => m.UserId == request.CreatedById && !m.IsDeleted));

                if (!isMember)
                {
                    return new ResponseHttp
                    {
                        FailMessages = "Access denied",
                        Status = StatusCodes.Status403Forbidden
                    };
                }

                // ✅ Validate Gherkin
                if (!_gherkinParser.ValidateSyntax(request.GherkinContent, out var errors))
                {
                    return new ResponseHttp
                    {
                        FailMessages = string.Join(", ", errors),
                        Status = StatusCodes.Status400BadRequest
                    };
                }

                var scenario = new Scenario
                {
                    Id = Guid.NewGuid(),
                    FeatureId = request.FeatureId,
                    Title = request.Title,
                    Description = request.Description,
                    GherkinContent = request.GherkinContent,
                    Status = request.Status,
                    CurrentVersion = 1,
                    CreatedDate = DateTime.UtcNow,
                    CreatedById = request.CreatedById.ToString()
                };

                // ✅ Parse steps
                var (_, parsed) = _gherkinParser.ParseFeatureContent(request.GherkinContent);

                if (parsed.Any())
                {
                    int order = 0;
                    foreach (var step in parsed.First().Steps)
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
                }

                // ✅ FIX TAGS
                if (request.Tags != null && request.Tags.Any())
                {
                    foreach (var tagName in request.Tags.Distinct())
                    {
                        var tag = await _scenarioRepository.GetOrCreateTagAsync(tagName, projectId, ct);

                        scenario.ScenarioTags.Add(new ScenarioTag
                        {
                            ScenarioId = scenario.Id,
                            TagId = tag.Id
                        });
                    }
                }

                // ✅ Version initiale
                scenario.Versions.Add(new ScenarioVersion
                {
                    Id = Guid.NewGuid(),
                    VersionNumber = 1,
                    GherkinContent = request.GherkinContent,
                    ChangeDescription = "Initial version",
                    CreatedDate = DateTime.UtcNow,
                    CreatedById = request.CreatedById.ToString()
                });

                await _scenarioRepository.Post(scenario);
                await _scenarioRepository.SaveChange(ct);

                var created = await _scenarioRepository.GetFullAsync(scenario.Id, ct);

                return new ResponseHttp
                {
                    Resultat = _mapper.Map<ScenarioDetailDto>(created),
                    Status = StatusCodes.Status201Created
                };
            }
        }
    }
}