using Domain.Common;

namespace Domain.Entities.Scenarios
{
    public class ScenarioTag : Entity
    {
        public Guid ScenarioId { get; set; }
        public Guid TagId { get; set; }

        // Navigation
        public Scenario Scenario { get; set; } = null!;
        public Tag Tag { get; set; } = null!;
    }

}
