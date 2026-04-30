using Domain.Common;
using Domain.Entities.Scenarios;
using Domain.Enums;

namespace Domain.Entities.Execution
{
    public class TestResult : Entity
    {
        public Guid ExecutionId { get; set; }
        public Guid ScenarioId { get; set; }
        public TestStatus Status { get; set; }
        public string? ErrorMessage { get; set; }
        public TimeSpan Duration { get; set; }
        public DateTime StartedAt { get; set; }
        public DateTime CompletedAt { get; set; }

        // Navigation
        public TestExecution Execution { get; set; } = null!;
        public Scenario Scenario { get; set; } = null!;
        public ICollection<StepResult> StepResults { get; set; } = new List<StepResult>();
    }
}