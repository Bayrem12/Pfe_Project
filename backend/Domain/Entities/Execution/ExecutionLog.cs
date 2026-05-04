using Domain.Common;
using Domain.Enums;

namespace Domain.Entities.Execution
{
    public class ExecutionLog : Entity
    {
        public Guid ExecutionId { get; set; }
        public Domain.Enums.LogLevel Level { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? Details { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        // Navigation
        public TestExecution Execution { get; set; } = null!;
    }
}