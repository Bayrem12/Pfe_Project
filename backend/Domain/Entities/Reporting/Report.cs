using Domain.Common;
using Domain.Entities.Execution;
using Domain.Entities.Identity;
using Domain.Enums;

namespace Domain.Entities.Reporting
{
    public class Report : Entity
    {
        public Guid ExecutionId { get; set; }
        public ReportFormat Format { get; set; }
        public string FilePath { get; set; } = string.Empty;
        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
        public Guid GeneratedById { get; set; }

        // Navigation
        public TestExecution Execution { get; set; } = null!;
        public User GeneratedBy { get; set; } = null!;
    }
}