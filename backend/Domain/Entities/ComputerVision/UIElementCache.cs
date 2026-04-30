using Domain.Common;
using Domain.Entities.ProjectManagement;

namespace Domain.Entities.ComputerVision
{
    public class UIElementCache : Entity
    {
        public Guid ProjectId { get; set; }
        public string PageUrl { get; set; } = string.Empty;
        public string ElementIdentifier { get; set; } = string.Empty;
        public string SelectorType { get; set; } = string.Empty;
        public string SelectorValue { get; set; } = string.Empty;
        public double Confidence { get; set; }
        public DateTime LastDetectedAt { get; set; } = DateTime.UtcNow;
        public int DetectionCount { get; set; } = 1;

        // Navigation
        public Project Project { get; set; } = null!;
    }

}