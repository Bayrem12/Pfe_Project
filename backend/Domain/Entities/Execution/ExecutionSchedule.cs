using Domain.Common;
using Domain.Entities.Identity;
using Domain.Entities.Scenarios;

namespace Domain.Entities.Execution
{
    public class ExecutionSchedule : Entity
    {
        public Guid TestSuiteId { get; set; }
        public string CronExpression { get; set; } = string.Empty;
        public Guid EnvironmentId { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime? LastRunAt { get; set; }
        public DateTime? NextRunAt { get; set; }

        // Navigation
        public TestSuite TestSuite { get; set; } = null!;
        public TestData.Environment Environment { get; set; } = null!;
        public User Users { get; set; } = null!;
    }
}