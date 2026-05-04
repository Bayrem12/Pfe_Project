using Domain.Common;

namespace Domain.Entities.Execution
{
    public class Screenshot : Entity
    {
        public Guid StepResultId { get; set; }
        public string FilePath { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public DateTime CapturedAt { get; set; } = DateTime.UtcNow;
        public long FileSize { get; set; }

        public StepResult StepResult { get; set; } = null!;
    }
}