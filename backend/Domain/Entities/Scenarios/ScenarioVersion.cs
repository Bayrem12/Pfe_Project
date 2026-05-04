using Domain.Common;
using Domain.Entities.Identity;

namespace Domain.Entities.Scenarios
{
    public class ScenarioVersion : Entity
    {
        public Guid ScenarioId { get; set; }
        public int VersionNumber { get; set; }
        public string GherkinContent { get; set; } = string.Empty;
        public string ChangeDescription { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public Scenario Scenario { get; set; } = null!;
        public User? User { get; set; } = null!;
    }

}