// Repository implementation for Dashboard.
// This is the most complex repository because it queries ACROSS multiple tables
// (Projects, Scenarios, TestExecutions, TestResults, AuditLogs) to aggregate statistics.
//
// Unlike NLP repositories that extend GenericRepository<T>,
// this one directly uses the DbContext to query multiple DbSets.

using Application.Features.DashboardFeature.Dtos;
using Application.Interfaces;
using Domain.Common;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Persistance.Data;

namespace Persistance.Repositories
{
    /// <summary>
    /// Concrete repository for dashboard data aggregation.
    /// Reads from multiple database tables to compute statistics.
    /// All queries are read-only (AsNoTracking) for performance.
    /// </summary>
    public class DashboardRepository : IDashboardRepository
    {
        // We inject the DbContext directly (not through GenericRepository)
        // because we need to query multiple entity types.
        private readonly TestAutoumatisationContext _context;

        public DashboardRepository(TestAutoumatisationContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Aggregates global platform statistics.
        /// Counts projects, scenarios, executions, and calculates overall pass rate.
        /// </summary>
        public async Task<DashboardSummaryDto> GetSummaryAsync(CancellationToken cancellationToken)
        {
            // Count non-deleted projects
            var totalProjects = await _context.Projects
                .AsNoTracking()
                .CountAsync(p => !p.IsDeleted, cancellationToken);

            // Count active projects (IsActive = true AND not deleted)
            var activeProjects = await _context.Projects
                .AsNoTracking()
                .CountAsync(p => p.IsActive && !p.IsDeleted, cancellationToken);

            // Count non-deleted scenarios
            var totalScenarios = await _context.Scenarios
                .AsNoTracking()
                .CountAsync(s => !s.IsDeleted, cancellationToken);

            // Count non-deleted test executions
            var totalExecutions = await _context.TestExecutions
                .AsNoTracking()
                .CountAsync(e => !e.IsDeleted, cancellationToken);

            // Count pending/running executions
            var pendingExecutions = await _context.TestExecutions
                .AsNoTracking()
                .CountAsync(e => !e.IsDeleted &&
                    (e.Status == ExecutionStatus.Pending || e.Status == ExecutionStatus.Running),
                    cancellationToken);

            // Calculate overall pass rate from TestResults
            // Pass rate = (passed tests / total tests) * 100
            var totalResults = await _context.TestResults
                .AsNoTracking()
                .CountAsync(r => !r.IsDeleted, cancellationToken);

            var passedResults = await _context.TestResults
                .AsNoTracking()
                .CountAsync(r => !r.IsDeleted && r.Status == TestStatus.Passed, cancellationToken);

            // Avoid division by zero: if no results, pass rate is 0
            var overallPassRate = totalResults > 0
                ? Math.Round((double)passedResults / totalResults * 100, 2)
                : 0;

            return new DashboardSummaryDto
            {
                TotalProjects = totalProjects,
                ActiveProjects = activeProjects,
                TotalScenarios = totalScenarios,
                TotalExecutions = totalExecutions,
                PendingExecutions = pendingExecutions,
                OverallPassRate = overallPassRate
            };
        }

        /// <summary>
        /// Gets statistics for a single project.
        /// Joins Project → Scenarios → TestExecutions → TestResults to compute numbers.
        /// </summary>
        public async Task<ProjectStatisticsDto?> GetProjectStatisticsAsync(Guid projectId, CancellationToken cancellationToken)
        {
            // First, check if the project exists
            var project = await _context.Projects
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == projectId && !p.IsDeleted, cancellationToken);

            if (project == null) return null;

            // Count scenarios that belong to this project (server-side, no Include needed)
            // Hierarchy: Project → Module → Feature → Scenario
            var totalScenarios = await _context.Scenarios
                .AsNoTracking()
                .CountAsync(s => !s.IsDeleted
                             && s.Feature!.Module!.ProjectId == projectId,
                    cancellationToken);

            // Server-side IQueryable for executions in this project (not yet materialized)
            var executionQuery = _context.TestExecutions
                .AsNoTracking()
                .Where(e => !e.IsDeleted
                         && e.Scenario!.Feature!.Module!.ProjectId == projectId);

            var totalExecutions = await executionQuery.CountAsync(cancellationToken);

            // Use subquery to count test results server-side (avoids loading any entities)
            var executionIdsQuery = executionQuery.Select(e => e.Id);

            var passedTests = await _context.TestResults
                .AsNoTracking()
                .CountAsync(r => !r.IsDeleted
                             && r.Status == TestStatus.Passed
                             && executionIdsQuery.Contains(r.ExecutionId),
                    cancellationToken);

            var failedTests = await _context.TestResults
                .AsNoTracking()
                .CountAsync(r => !r.IsDeleted
                             && r.Status == TestStatus.Failed
                             && executionIdsQuery.Contains(r.ExecutionId),
                    cancellationToken);

            var totalTests = passedTests + failedTests;

            // Calculate pass rate
            var passRate = totalTests > 0
                ? Math.Round((double)passedTests / totalTests * 100, 2)
                : 0;

            // Project only the columns needed for average duration and last execution date
            var executionMeta = await executionQuery
                .Select(e => new { e.StartedAt, e.Duration })
                .ToListAsync(cancellationToken);

            var avgExecutionTime = executionMeta
                .Where(e => e.Duration.HasValue)
                .Select(e => e.Duration!.Value.TotalSeconds)
                .DefaultIfEmpty(0)
                .Average();

            var lastExecutionDate = executionMeta
                .OrderByDescending(e => e.StartedAt)
                .Select(e => (DateTime?)e.StartedAt)
                .FirstOrDefault();

            return new ProjectStatisticsDto
            {
                ProjectId = projectId,
                ProjectName = project.Name,
                TotalScenarios = totalScenarios,
                TotalExecutions = totalExecutions,
                PassedTests = passedTests,
                FailedTests = failedTests,
                PassRate = passRate,
                AverageExecutionTime = Math.Round(avgExecutionTime, 2),
                LastExecutionDate = lastExecutionDate
            };
        }

        /// <summary>
        /// Gets daily execution trends for a project.
        /// Groups executions by date and counts pass/fail per day.
        /// This data feeds into line/bar charts on the Angular dashboard.
        /// </summary>
        public async Task<ExecutionTrendsDto> GetExecutionTrendsAsync(Guid projectId, int days, CancellationToken cancellationToken)
        {
            // Calculate the start date (e.g., 30 days ago)
            var startDate = DateTime.UtcNow.AddDays(-days).Date;

            // Get execution IDs and dates for this project — lightweight projection, no Include
            var executionsInRange = await _context.TestExecutions
                .AsNoTracking()
                .Where(e => !e.IsDeleted
                         && e.StartedAt >= startDate
                         && e.Scenario!.Feature!.Module!.ProjectId == projectId)
                .Select(e => new { e.Id, Date = e.StartedAt.Date })
                .ToListAsync(cancellationToken);

            if (!executionsInRange.Any())
            {
                return new ExecutionTrendsDto
                {
                    ProjectId = projectId,
                    Days = days,
                    DataPoints = Enumerable.Range(0, days)
                        .Select(offset => new TrendDataPointDto
                        {
                            Date = startDate.AddDays(offset),
                            TotalExecutions = 0,
                            Passed = 0,
                            Failed = 0,
                            PassRate = 0
                        }).ToList()
                };
            }

            var executionIdsList = executionsInRange.Select(e => e.Id).ToList();

            // Load only ExecutionId + Status (two columns) for all relevant results
            var results = await _context.TestResults
                .AsNoTracking()
                .Where(r => !r.IsDeleted && executionIdsList.Contains(r.ExecutionId))
                .Select(r => new { r.ExecutionId, r.Status })
                .ToListAsync(cancellationToken);

            // Group by day in memory (minimal data: only IDs, dates, statuses)
            var grouped = executionsInRange
                .GroupBy(e => e.Date)
                .Select(group =>
                {
                    var dayExecutionIds = group.Select(e => e.Id).ToHashSet();
                    var dayResults = results.Where(r => dayExecutionIds.Contains(r.ExecutionId)).ToList();
                    var passedForDay = dayResults.Count(r => r.Status == TestStatus.Passed);
                    var failedForDay = dayResults.Count(r => r.Status == TestStatus.Failed);
                    var totalForDay = dayResults.Count;

                    return new TrendDataPointDto
                    {
                        Date = group.Key,
                        TotalExecutions = group.Count(),
                        Passed = passedForDay,
                        Failed = failedForDay,
                        PassRate = totalForDay > 0
                            ? Math.Round((double)passedForDay / totalForDay * 100, 2)
                            : 0
                    };
                })
                .OrderBy(dp => dp.Date)
                .ToList();

            // Fill in missing dates with zero values (so the chart has continuous data)
            var allDates = Enumerable.Range(0, days)
                .Select(offset => startDate.AddDays(offset))
                .ToList();

            var dataPoints = allDates.Select(date =>
            {
                // Check if we have data for this date
                var existing = grouped.FirstOrDefault(dp => dp.Date.Date == date.Date);
                return existing ?? new TrendDataPointDto
                {
                    Date = date,
                    TotalExecutions = 0,
                    Passed = 0,
                    Failed = 0,
                    PassRate = 0
                };
            }).ToList();

            return new ExecutionTrendsDto
            {
                ProjectId = projectId,
                Days = days,
                DataPoints = dataPoints
            };
        }

        /// <summary>
        /// Gets paginated audit logs, sorted by most recent first.
        /// Uses the PagedList helper to handle pagination metadata.
        /// </summary>
        public async Task<PagedList<AuditLogDto>> GetAuditLogsAsync(int page, int pageSize, CancellationToken cancellationToken)
        {
            // Count total records for pagination metadata
            var totalCount = await _context.AuditLogs
                .AsNoTracking()
                .CountAsync(a => !a.IsDeleted, cancellationToken);

            // Fetch the page of audit logs, ordered by most recent first
            var logs = await _context.AuditLogs
                .AsNoTracking()
                .Where(a => !a.IsDeleted)
                .OrderByDescending(a => a.Timestamp)   // Most recent first
                .Skip((page - 1) * pageSize)            // Skip previous pages
                .Take(pageSize)                          // Take only this page's items
                .Select(a => new AuditLogDto             // Project directly to DTO (efficient: only selects needed columns)
                {
                    Id = a.Id,
                    UserId = a.UserId,
                    Action = a.Action,
                    EntityType = a.EntityType,
                    EntityId = a.EntityId,
                    OldValues = a.OldValues,
                    NewValues = a.NewValues,
                    Timestamp = a.Timestamp,
                    IpAddress = a.IpAddress
                })
                .ToListAsync(cancellationToken);

            // Wrap in PagedList with pagination metadata
            return new PagedList<AuditLogDto>(logs, totalCount, page, pageSize);
        }
    }
}
