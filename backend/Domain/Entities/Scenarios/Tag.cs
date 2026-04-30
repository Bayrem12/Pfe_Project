using Domain.Common;
using Domain.Entities.ProjectManagement;

namespace Domain.Entities.Scenarios
{
    public class Tag : Entity
    {
        public string Name { get; set; } = default!;
        public string? Color { get; set; }
        public string? Description { get; set; }
        public Guid ProjectId { get; set; }
        public Project Project { get; set; } = null!;

        public ICollection<ScenarioTag> ScenarioTags { get; set; } = new List<ScenarioTag>();
    }
}