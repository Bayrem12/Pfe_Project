using Domain.Common;
using Domain.Entities.ProjectManagement;

namespace Domain.Entities.ComputerVision
{
    public class UIPattern : Entity
    {
        public Guid ProjectId { get; set; }
        public string PatternName { get; set; } = string.Empty;
        public string PatternType { get; set; } = string.Empty;
        public string TemplateData { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public int UsageCount { get; set; }

        // Navigation
        public Project Project { get; set; } = null!;
    }
}