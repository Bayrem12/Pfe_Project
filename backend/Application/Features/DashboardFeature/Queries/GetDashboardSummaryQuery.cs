// QUERY: Get global dashboard summary
// Endpoint: GET /api/Dashboard/summary
//
// This is a QUERY (not a Command) because it only READS data, never writes.
// In CQRS: Commands = write operations, Queries = read operations.
using Application.Interfaces;
using Application.Setting;
using MediatR;
using Microsoft.AspNetCore.Http;

namespace Application.Features.DashboardFeature.Queries
{
    /// <summary>
    /// Query to get the global dashboard summary.
    /// No parameters needed — it returns platform-wide statistics.
    /// "record" with no properties = a parameterless query.
    /// </summary>
    public record GetDashboardSummaryQuery() : IRequest<ResponseHttp>
    {
        public class GetDashboardSummaryQueryHandler : IRequestHandler<GetDashboardSummaryQuery, ResponseHttp>
        {
            private readonly IDashboardRepository _dashboardRepository;

            public GetDashboardSummaryQueryHandler(IDashboardRepository dashboardRepository)
            {
                _dashboardRepository = dashboardRepository;
            }

            public async Task<ResponseHttp> Handle(GetDashboardSummaryQuery request, CancellationToken cancellationToken)
            {
                try
                {
                    // The repository handles the complex aggregation queries
                    var summary = await _dashboardRepository.GetSummaryAsync(cancellationToken);

                    return new ResponseHttp
                    {
                        Resultat = summary,
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
