using Domain.Common;
using Domain.Entities.Execution;
using Domain.Entities.Identity;
using Domain.Entities.ProjectManagement;
using Domain.Enums;

namespace Domain.Entities.Scenarios
{
    public class Scenario : Entity
    {
        
        public Guid FeatureId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string GherkinContent { get; set; } = string.Empty;
        public ScenarioStatus Status { get; set; } = ScenarioStatus.Draft;
        public int CurrentVersion { get; set; } = 1;

        // AI quality analysis
        public int? QualityScore { get; set; }
        public string? QualityLabel { get; set; }
        public DateTime? LastAnalyzedAt { get; set; }

        // Navigation
        public Feature Feature { get; set; } = null!;
        public User? User { get; set; } = null!;
        public ICollection<Step> Steps { get; set; } = new List<Step>();
        public ICollection<ScenarioVersion> Versions { get; set; } = new List<ScenarioVersion>();
        public ICollection<ScenarioTag> ScenarioTags { get; set; } = new List<ScenarioTag>();
        public ICollection<TestSuiteScenario> TestSuiteScenarios { get; set; } = new List<TestSuiteScenario>();
        public ICollection<TestResult> TestResults { get; set; } = new List<TestResult>();
    }
}