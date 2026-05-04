// QUERY: Get paginated audit logs
// Endpoint: GET /api/Dashboard/audit-logs?page=1&pageSize=50
//
// Audit logs track every important action in the system (who did what, when).
// They're paginated because there can be thousands of entries.
using Application.Interfaces;
using Application.Setting;
using MediatR;
using Microsoft.AspNetCore.Http;

namespace Application.Features.DashboardFeature.Queries
{
    /// <summary>
    /// Query to get paginated audit logs.
    /// Page = which page to show (1-based), PageSize = how many items per page.
    /// Default: page 1, 50 items per page.
    /// </summary>
    public record GetAuditLogsQuery(int Page = 1, int PageSize = 50) : IRequest<ResponseHttp>
    {
        public class GetAuditLogsQueryHandler : IRequestHandler<GetAuditLogsQuery, ResponseHttp>
        {
            private readonly IDashboardRepository _dashboardRepository;

            public GetAuditLogsQueryHandler(IDashboardRepository dashboardRepository)
            {
                _dashboardRepository = dashboardRepository;
            }

            public async Task<ResponseHttp> Handle(GetAuditLogsQuery request, CancellationToken cancellationToken)
            {
                try
                {
                    // Ensure valid pagination values
                    var page = Math.Max(1, request.Page);           // Minimum page = 1
                    var pageSize = Math.Clamp(request.PageSize, 1, 100); // Between 1 and 100

                    var auditLogs = await _dashboardRepository.GetAuditLogsAsync(
                        page, pageSize, cancellationToken);

                    return new ResponseHttp
                    {
                        Resultat = auditLogs,
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
