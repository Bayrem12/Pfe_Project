using Domain.Common;
using Domain.Entities.Identity;
using Domain.Entities.Scenarios;
using Domain.Enums;

namespace Domain.Entities.Execution
{
    public class TestExecution : Entity
    {
        public Guid? ScenarioId { get; set; }
        public Guid? TestSuiteId { get; set; }
        public Guid EnvironmentId { get; set; }
        public ExecutionStatus Status { get; set; } = ExecutionStatus.Pending;
        public DateTime StartedAt { get; set; } = DateTime.UtcNow;
        public DateTime? CompletedAt { get; set; }
        public TimeSpan? Duration { get; set; }
        public Guid ExecutedById { get; set; }
        public string BrowserType { get; set; } = "Chromium";
        public bool IsHeadless { get; set; } = true;

        // Navigation
        public Scenario? Scenario { get; set; }
        public TestSuite? TestSuite { get; set; }
        public TestData.Environment Environment { get; set; } = null!;
        public User ExecutedBy { get; set; } = null!;
        public ICollection<TestResult> TestResults { get; set; } = new List<TestResult>();
        public ICollection<ExecutionLog> Logs { get; set; } = new List<ExecutionLog>();
    }
}