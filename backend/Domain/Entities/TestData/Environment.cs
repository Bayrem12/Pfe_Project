using Domain.Common;
using Domain.Entities.ProjectManagement;

namespace Domain.Entities.TestData
{
    public class Environment : Entity
    {
        public Guid ProjectId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string BaseUrl { get; set; } = string.Empty;
        public bool IsDefault { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public Project Project { get; set; } = null!;
    }
}