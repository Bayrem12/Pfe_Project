// QUERY: Get execution trends for a project over time
// Endpoint: GET /api/Dashboard/projects/{projectId}/trends?days=30
//
// Returns daily data points showing pass/fail counts that Angular can use
// to render trend charts (line charts, bar charts, etc.)
using Application.Interfaces;
using Application.Setting;
using MediatR;
using Microsoft.AspNetCore.Http;

namespace Application.Features.DashboardFeature.Queries
{
    /// <summary>
    /// Query to get execution trends over a period of days.
    /// Days = 30 means "give me the last 30 days of execution data".
    /// </summary>
    public record GetExecutionTrendsQuery(Guid ProjectId, int Days = 30) : IRequest<ResponseHttp>
    {
        public class GetExecutionTrendsQueryHandler : IRequestHandler<GetExecutionTrendsQuery, ResponseHttp>
        {
            private readonly IDashboardRepository _dashboardRepository;

            public GetExecutionTrendsQueryHandler(IDashboardRepository dashboardRepository)
            {
                _dashboardRepository = dashboardRepository;
            }

            public async Task<ResponseHttp> Handle(GetExecutionTrendsQuery request, CancellationToken cancellationToken)
            {
                try
                {
                    // Clamp days between 1 and 365 to prevent unreasonable queries
                    var days = Math.Clamp(request.Days, 1, 365);

                    var trends = await _dashboardRepository.GetExecutionTrendsAsync(
                        request.ProjectId, days, cancellationToken);

                    return new ResponseHttp
                    {
                        Resultat = trends,
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
