using Application.Interfaces;
using Application.Setting;
using Domain.Entities.Scenarios;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.ScenariosFeature.Commands
{
    public record DeleteScenarioCommand(Guid Id, Guid DeletedById) : IRequest<ResponseHttp>
    {
        public class DeleteScenarioCommandHandler : IRequestHandler<DeleteScenarioCommand, ResponseHttp>
        {
            private readonly IScenarioRepository _scenarioRepository;
            private readonly IProjectRepository _projectRepository;

            public DeleteScenarioCommandHandler(
                IScenarioRepository scenarioRepository,
                IProjectRepository projectRepository)
            {
                _scenarioRepository = scenarioRepository;
                _projectRepository = projectRepository;
            }

            public async Task<ResponseHttp> Handle(DeleteScenarioCommand request, CancellationToken cancellationToken)
            {
                try
                {
                    // ✅ FIX INCLUDE
                    var scenario = await _scenarioRepository.GetByIdWithIncludes(
                        request.Id,
                        q => q
                            .Include(s => s.Feature)
                                .ThenInclude(f => f.Module)
                                    .ThenInclude(m => m.Project),
                        cancellationToken);

                    if (scenario == null)
                    {
                        return new ResponseHttp
                        {
                            FailMessages = "Scenario not found",
                            Status = StatusCodes.Status404NotFound
                        };
                    }

                    var projectId = scenario.Feature?.Module?.ProjectId;

                    if (projectId == null)
                    {
                        return new ResponseHttp
                        {
                            FailMessages = "Unable to determine project ownership",
                            Status = StatusCodes.Status400BadRequest
                        };
                    }

                    // ✅ FIX MEMBERSHIP
                    var project = await _projectRepository.GetProjectWithMembersAsync(
                        projectId.Value,
                        cancellationToken);

                    if (project == null)
                    {
                        return new ResponseHttp
                        {
                            FailMessages = "Project not found",
                            Status = StatusCodes.Status404NotFound
                        };
                    }

                    var isMember = project.UserId == request.DeletedById ||
                        project.Members.Any(m =>
                        m.UserId == request.DeletedById && !m.IsDeleted);

                    if (!isMember)
                    {
                        return new ResponseHttp
                        {
                            FailMessages = "You are not a member of this project",
                            Status = StatusCodes.Status403Forbidden
                        };
                    }

                    // ✅ FIX SOFT DELETE SIMPLE
                    scenario.IsDeleted = true;
                    scenario.DeletedDate = DateTime.UtcNow;

                    await _scenarioRepository.SaveChange(cancellationToken);

                    return new ResponseHttp
                    {
                        Resultat = true,
                        Status = StatusCodes.Status204NoContent
                    };
                }
                catch (Exception ex)
                {
                    return new ResponseHttp
                    {
                        FailMessages = ex.InnerException?.Message ?? ex.Message,
                        Status = StatusCodes.Status400BadRequest
                    };
                }
            }
        }
    }
}