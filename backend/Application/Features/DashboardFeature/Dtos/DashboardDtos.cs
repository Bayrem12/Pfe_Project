// DTOs for the Dashboard feature.
// These shape the data returned by the 4 Dashboard endpoints.
// Each DTO is designed to match what the Angular frontend needs to display.

namespace Application.Features.DashboardFeature.Dtos
{
    /// <summary>
    /// GET /api/Dashboard/summary
    /// Global dashboard summary — gives a bird's-eye view of the entire platform.
    /// The Angular dashboard home page will display these numbers.
    /// </summary>
    public class DashboardSummaryDto
    {
        // Total number of projects in the system
        public int TotalProjects { get; set; }

        // Total number of scenarios (test cases) across all projects
        public int TotalScenarios { get; set; }

        // Total number of test executions (how many times tests have been run)
        public int TotalExecutions { get; set; }

        // Overall pass rate across all executions (0.0 to 100.0)
        public double OverallPassRate { get; set; }

        // Number of active (non-archived) projects
        public int ActiveProjects { get; set; }

        // Number of executions currently running or pending
        public int PendingExecutions { get; set; }
    }

    /// <summary>
    /// GET /api/Dashboard/projects/{projectId}/statistics
    /// Statistics specific to one project — scenarios, executions, pass/fail rates.
    /// </summary>
    public class ProjectStatisticsDto
    {
        // The project's ID
        public Guid ProjectId { get; set; }

        // The project's name
        public string ProjectName { get; set; } = string.Empty;

        // Total scenarios in this project
        public int TotalScenarios { get; set; }

        // Total executions for this project
        public int TotalExecutions { get; set; }

        // Number of tests that passed
        public int PassedTests { get; set; }

        // Number of tests that failed
        public int FailedTests { get; set; }

        // Pass rate for this project (0.0 to 100.0)
        public double PassRate { get; set; }

        // Average time (in seconds) an execution takes
        public double AverageExecutionTime { get; set; }

        // When the last execution was run
        public DateTime? LastExecutionDate { get; set; }
    }

    /// <summary>
    /// GET /api/Dashboard/projects/{projectId}/trends?days=30
    /// Execution trends over time — used to draw charts on the Angular dashboard.
    /// </summary>
    public class ExecutionTrendsDto
    {
        // The project ID these trends belong to
        public Guid ProjectId { get; set; }

        // How many days of data this covers
        public int Days { get; set; }

        // Daily trend data points for the chart
        public List<TrendDataPointDto> DataPoints { get; set; } = new();
    }

    /// <summary>
    /// A single data point in the trends chart.
    /// Each point represents one day's execution statistics.
    /// </summary>
    public class TrendDataPointDto
    {
        // The date for this data point
        public DateTime Date { get; set; }

        // How many tests ran on this date
        public int TotalExecutions { get; set; }

        // How many tests passed
        public int Passed { get; set; }

        // How many tests failed
        public int Failed { get; set; }

        // Pass rate for this date (0.0 to 100.0)
        public double PassRate { get; set; }
    }

    /// <summary>
    /// GET /api/Dashboard/audit-logs?page=1&pageSize=50
    /// Audit log entry — tracks who did what and when in the system.
    /// Used for security and accountability.
    /// </summary>
    public class AuditLogDto
    {
        // Unique ID of the audit log entry
        public Guid Id { get; set; }

        // ID of the user who performed the action
        public Guid UserId { get; set; }

        // What action was performed — "Created", "Updated", "Deleted"
        public string Action { get; set; } = string.Empty;

        // What type of entity was affected — "Scenario", "Project", "TestExecution"
        public string EntityType { get; set; } = string.Empty;

        // The ID of the affected entity (nullable because some actions don't target a specific entity)
        public Guid? EntityId { get; set; }

        // JSON string of old values (before the change)
        public string? OldValues { get; set; }

        // JSON string of new values (after the change)
        public string? NewValues { get; set; }

        // When this action happened
        public DateTime Timestamp { get; set; }

        // IP address of the user who performed the action
        public string IpAddress { get; set; } = string.Empty;
    }
}
