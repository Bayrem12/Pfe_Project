using Domain.Common;

namespace Domain.Entities.Scenarios
{
    public class TestSuiteScenario :Entity
    {
        public Guid TestSuiteId { get; set; }
        public Guid ScenarioId { get; set; }
        public int DisplayOrder { get; set; }

        // Navigation
        public TestSuite TestSuite { get; set; } = null!;
        public Scenario Scenario { get; set; } = null!;
    }

}
