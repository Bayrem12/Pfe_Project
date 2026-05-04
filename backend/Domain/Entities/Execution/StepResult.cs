using Domain.Common;
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

        // AI failure analysis (rule-based diagnosis produced by the IA agent
        // when the step fails).  Stored as JSON so the schema can evolve
        // without further migrations.  Null on passing steps.
        public string? AiAnalysisJson { get; set; }

        public TestResult TestResult { get; set; } = null!;
        public Step Step { get; set; } = null!;
        public Screenshot? Screenshot { get; set; }
    }
}