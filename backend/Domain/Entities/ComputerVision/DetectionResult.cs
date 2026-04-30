using Domain.Common;
using Domain.Entities.Execution;
using Domain.Enums;

namespace Domain.Entities.ComputerVision
{
    public class DetectionResult : Entity
    {
        public Guid StepResultId { get; set; }
        public string ElementType { get; set; } = string.Empty;
        public string BoundingBox { get; set; } = string.Empty; // JSON: {x, y, width, height}
        public double Confidence { get; set; }
        public DetectionMethod Method { get; set; }
        public string ScreenshotPath { get; set; } = string.Empty;
        public DateTime DetectedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public StepResult StepResult { get; set; } = null!;
    }

}
