using Domain.Common;
using Domain.Entities.Scenarios;

namespace Domain.Entities.ProjectManagement

{

    public class Feature : Entity
    {
        public Guid ModuleId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int DisplayOrder { get; set; }

        // Navigation
        public Module Module { get; set; } = null!;
        public ICollection<Scenario> Scenarios { get; set; } = new List<Scenario>();
    }

}
