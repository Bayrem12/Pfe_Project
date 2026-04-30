using Domain.Common;
using Domain.Entities.ComputerVision;
using Domain.Entities.Scenarios;
using Domain.Enums;

namespace Domain.Entities.Execution
{
    public class StepResult : Entity
    {
        public Guid TestResultId { get; set; }
        public Guid StepId { get; set; }
        public StepStatus Status { get; set; }
        public string? ErrorMessage { get; set; }
        public TimeSpan Duration { get; set; }

        public string ActionPerformed { get; set; } = string.Empty;
        public string SelectorUsed { get; set; } = string.Empty;

        public TestResult TestResult { get; set; } = null!;
        public Step Step { get; set; } = null!;
        public Screenshot? Screenshot { get; set; }
    }
}