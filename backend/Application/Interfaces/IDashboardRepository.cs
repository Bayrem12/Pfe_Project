// Repository interface for Dashboard-specific queries.
// Unlike NLP repositories which extend IGenericRepository<T> (CRUD for one entity),
// the Dashboard repository queries ACROSS multiple entities (Projects, Scenarios, Executions, etc.)
// So we create a standalone interface (not extending IGenericRepository).

using Application.Features.DashboardFeature.Dtos;
using Domain.Common;

namespace Application.Interfaces
{
    /// <summary>
    /// Repository for dashboard data aggregation queries.
    /// This is READ-ONLY — dashboards don't write data, they only read and aggregate.
    /// It queries across multiple database tables to build summary statistics.
    /// </summary>
    public interface IDashboardRepository
    {
        /// <summary>
        /// Gets the global dashboard summary (total projects, scenarios, executions, pass rate).
        /// </summary>
        Task<DashboardSummaryDto> GetSummaryAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Gets statistics for a specific project (pass/fail counts, average execution time, etc.).
        /// </summary>
        Task<ProjectStatisticsDto?> GetProjectStatisticsAsync(Guid projectId, CancellationToken cancellationToken);

        /// <summary>
        /// Gets execution trends for a project over a number of days.
        /// Returns daily data points for charts.
        /// </summary>
        Task<ExecutionTrendsDto> GetExecutionTrendsAsync(Guid projectId, int days, CancellationToken cancellationToken);

        /// <summary>
        /// Gets paginated audit logs.
        /// PagedList handles the pagination metadata (CurrentPage, TotalPages, HasNext, etc.)
        /// </summary>
        Task<PagedList<AuditLogDto>> GetAuditLogsAsync(int page, int pageSize, CancellationToken cancellationToken);
    }
}
