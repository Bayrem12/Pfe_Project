// QUERY: Get statistics for a specific project
// Endpoint: GET /api/Dashboard/projects/{projectId}/statistics
using Application.Interfaces;
using Application.Setting;
using MediatR;
using Microsoft.AspNetCore.Http;

namespace Application.Features.DashboardFeature.Queries
{
    /// <summary>
    /// Query to get project-specific statistics.
    /// Takes a ProjectId to filter the data for one project.
    /// </summary>
    public record GetProjectStatisticsQuery(Guid ProjectId) : IRequest<ResponseHttp>
    {
        public class GetProjectStatisticsQueryHandler : IRequestHandler<GetProjectStatisticsQuery, ResponseHttp>
        {
            private readonly IDashboardRepository _dashboardRepository;

            public GetProjectStatisticsQueryHandler(IDashboardRepository dashboardRepository)
            {
                _dashboardRepository = dashboardRepository;
            }

            public async Task<ResponseHttp> Handle(GetProjectStatisticsQuery request, CancellationToken cancellationToken)
            {
                try
                {
                    var statistics = await _dashboardRepository.GetProjectStatisticsAsync(
                        request.ProjectId, cancellationToken);

                    // If no statistics found, the project doesn't exist
                    if (statistics == null)
                    {
                        return new ResponseHttp
                        {
                            Status = StatusCodes.Status400BadRequest,
                            FailMessages = $"Project with ID {request.ProjectId} not found."
                        };
                    }

                    return new ResponseHttp
                    {
                        Resultat = statistics,
                        Status = StatusCodes.Status200OK
                    };
                }
                catch (Exception ex)
                {
                    return new ResponseHttp
                    {
                        FailMessages = ex.Message,
                        Status = StatusCodes.Status400BadRequest
                    };
                }
            }
        }
    }
}
