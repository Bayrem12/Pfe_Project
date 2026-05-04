using Domain.Common;
using Domain.Entities.ProjectManagement;

namespace Domain.Entities.Scenarios
{
    public class TestSuite : Entity
    {
        public Guid ProjectId { get; set; }
        public string Name { get; set; } = default!;
        public string Description { get; set; } = default!;
        public Guid CreatedById { get; set; }
        public Project Project { get; set; } = null!;

        public ICollection<TestSuiteScenario> TestSuiteScenarios { get; set; } = new List<TestSuiteScenario>();
    }
}