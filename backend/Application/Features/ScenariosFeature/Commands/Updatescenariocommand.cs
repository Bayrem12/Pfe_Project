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
    public record UpdateScenarioCommand(
        Guid Id,
        string Title,
        string Description,
        string GherkinContent,
        string? ChangeDescription,
        Guid UpdatedById,
        ScenarioStatus? Status = null,
        List<string>? Tags = null
    ) : IRequest<ResponseHttp>
    {
        public class UpdateScenarioCommandHandler : IRequestHandler<UpdateScenarioCommand, ResponseHttp>
        {
            private readonly IScenarioRepository _scenarioRepository;
            private readonly IProjectRepository _projectRepository;
            private readonly IGherkinParserService _gherkinParser;
            private readonly IMapper _mapper;
            private readonly ITestTestAutoumatisationContext _context;

            public UpdateScenarioCommandHandler(
                IScenarioRepository scenarioRepository,
                IProjectRepository projectRepository,
                IGherkinParserService gherkinParser,
                IMapper mapper,
                ITestTestAutoumatisationContext context)
            {
                _scenarioRepository = scenarioRepository;
                _projectRepository = projectRepository;
                _gherkinParser = gherkinParser;
                _mapper = mapper;
                _context = context;
            }

            public async Task<ResponseHttp> Handle(UpdateScenarioCommand request, CancellationToken cancellationToken)
            {
                try
                {
                    var scenario = await _scenarioRepository.GetForUpdateAsync(request.Id, cancellationToken);

                    if (scenario == null)
                    {
                        return new ResponseHttp
                        {
                            Fail_Messages = "Scenario not found",
                            Status = StatusCodes.Status404NotFound
                        };
                    }

                    var projectId = scenario.Feature?.Module?.ProjectId;
                    if (projectId == null)
                    {
                        return new ResponseHttp
                        {
                            Fail_Messages = "Unable to determine project ownership",
                            Status = StatusCodes.Status400BadRequest
                        };
                    }

                    var project = await _projectRepository.GetProjectWithMembersAsync(projectId.Value, cancellationToken);

                    if (project == null)
                    {
                        return new ResponseHttp
                        {
                            Fail_Messages = "Project not found",
                            Status = StatusCodes.Status404NotFound
                        };
                    }

                    var isMember = project.UserId == request.UpdatedById ||
                        project.Members.Any(m =>
                        m.UserId == request.UpdatedById && !m.IsDeleted);

                    if (!isMember)
                    {
                        return new ResponseHttp
                        {
                            Fail_Messages = "You are not a member of this project",
                            Status = StatusCodes.Status403Forbidden
                        };
                    }

                    // Validation Gherkin
                    if (!_gherkinParser.ValidateSyntax(request.GherkinContent, out var errors))
                    {
                        return new ResponseHttp
                        {
                            Fail_Messages = string.Join(", ", errors),
                            Status = StatusCodes.Status400BadRequest
                        };
                    }

                    // Update scenario properties
                    scenario.Title = request.Title;
                    scenario.Description = request.Description;
                    scenario.GherkinContent = request.GherkinContent;

                    if (request.Status.HasValue)
                    {
                        scenario.Status = request.Status.Value;
                    }

                    scenario.CurrentVersion++;
                    scenario.ModifiedDate = DateTime.UtcNow;
                    scenario.ModifiedById = request.UpdatedById.ToString();

                    // Soft-delete old active steps (already tracked via Include)
                    foreach (var oldStep in scenario.Steps.Where(s => !s.IsDeleted).ToList())
                    {
                        oldStep.IsDeleted = true;
                        oldStep.DeletedDate = DateTimeOffset.UtcNow;
                    }

                    // Parse new steps from Gherkin
                    var (_, parsedScenarios) = _gherkinParser.ParseFeatureContent(request.GherkinContent);

                    if (parsedScenarios.Any())
                    {
                        int order = 0;
                        foreach (var step in parsedScenarios.First().Steps)
                        {
                            // Add via DbSet directly — guarantees EntityState.Added
                            await _context.Steps.AddAsync(new Step
                            {
                                Id = Guid.NewGuid(),
                                ScenarioId = scenario.Id,
                                StepType = Enum.Parse<StepType>(step.Keyword, true),
                                Text = step.Text,
                                DisplayOrder = order++,
                                DataTable = step.DataTable,
                                CreatedDate = DateTime.UtcNow
                            }, cancellationToken);
                        }
                    }

                    // Create new version via DbSet directly
                    await _context.ScenarioVersions.AddAsync(new ScenarioVersion
                    {
                        Id = Guid.NewGuid(),
                        ScenarioId = scenario.Id,
                        VersionNumber = scenario.CurrentVersion,
                        GherkinContent = request.GherkinContent,
                        ChangeDescription = request.ChangeDescription ?? $"Update v{scenario.CurrentVersion}",
                        CreatedDate = DateTime.UtcNow,
                        CreatedById = request.UpdatedById.ToString()
                    }, cancellationToken);

                    // Update tags
                    if (request.Tags != null)
                    {
                        // Soft-delete existing ScenarioTags
                        var existingScenarioTags = await _context.ScenarioTags
                            .Where(st => st.ScenarioId == scenario.Id && !st.IsDeleted)
                            .ToListAsync(cancellationToken);

                        foreach (var st in existingScenarioTags)
                        {
                            st.IsDeleted = true;
                            st.DeletedDate = DateTimeOffset.UtcNow;
                        }

                        // Add new tags
                        foreach (var tagName in request.Tags.Distinct())
                        {
                            if (string.IsNullOrWhiteSpace(tagName)) continue;
                            var tag = await _scenarioRepository.GetOrCreateTagAsync(tagName, projectId.Value, cancellationToken);
                            await _context.ScenarioTags.AddAsync(new ScenarioTag
                            {
                                Id = Guid.NewGuid(),
                                ScenarioId = scenario.Id,
                                TagId = tag.Id,
                                CreatedDate = DateTime.UtcNow
                            }, cancellationToken);
                        }
                    }

                    await _context.SaveChangesAsync(cancellationToken);

                    // Reload with full includes for response
                    var updated = await _scenarioRepository.GetFullAsync(scenario.Id, cancellationToken);

                    return new ResponseHttp
                    {
                        Resultat = _mapper.Map<ScenarioDetailDto>(updated),
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