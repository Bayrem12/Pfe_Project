// Query to get all action mappings for a project.
// Follows CQRS pattern: Queries read data, Commands write data.

using Application.Features.NlpFeature.Dtos;
using Application.Interfaces;
using Application.Setting;
using MediatR;
using Microsoft.AspNetCore.Http;

namespace Application.Features.NlpFeature.Queries
{
    /// <summary>
    /// Query to retrieve all action mappings for a specific project.
    /// Route: GET /api/nlp/action-mappings/{projectId}
    /// </summary>
    public class GetActionMappingsByProjectQuery : IRequest<ResponseHttp>
    {
        public Guid ProjectId { get; set; }

        public GetActionMappingsByProjectQuery(Guid projectId)
        {
            ProjectId = projectId;
        }
    }

    /// <summary>
    /// Handler for GetActionMappingsByProjectQuery.
    /// Returns a list of ActionMappingDto for the specified project.
    /// </summary>
    public class GetActionMappingsByProjectQueryHandler : IRequestHandler<GetActionMappingsByProjectQuery, ResponseHttp>
    {
        private readonly IActionMappingRepository _actionMappingRepository;

        public GetActionMappingsByProjectQueryHandler(IActionMappingRepository actionMappingRepository)
        {
            _actionMappingRepository = actionMappingRepository;
        }

        public async Task<ResponseHttp> Handle(GetActionMappingsByProjectQuery request, CancellationToken cancellationToken)
        {
            try
            {
                // Get all action mappings for the project
                var mappings = await _actionMappingRepository.GetByProjectIdAsync(request.ProjectId, cancellationToken);

                // Map to DTOs
                var mappingDtos = mappings.Select(m => new ActionMappingDto
                {
                    Id = m.Id,
                    IntentPattern = m.IntentPattern,
                    ActionType = m.ActionType.ToString(),
                    SelectorStrategy = m.SelectorStrategy,
                    SelectorValue = m.SelectorValue,
                    Description = m.Description ?? string.Empty,
                    IsActive = m.IsActive
                }).ToList();

                return new ResponseHttp
                {
                    Status = StatusCodes.Status200OK,
                    Resultat = mappingDtos,
                    FailMessages = null
                };
            }
            catch (Exception ex)
            {
                return new ResponseHttp
                {
                    Status = StatusCodes.Status500InternalServerError,
                    FailMessages = $"Error retrieving action mappings: {ex.Message}"
                };
            }
        }
    }
}
