using Domain.Common;
using Domain.Entities.Identity;
using Domain.Entities.Scenarios;
using Domain.Entities.TestData;

namespace Domain.Entities.ProjectManagement
{
    public class Project : Entity
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string? Url { get; set; }
        public bool IsActive { get; set; } = true;
        public Guid? UserId { get; set; }

        // Navigation
        public User? User { get; set; }
        public ICollection<ProjectMember> Members { get; set; } = new List<ProjectMember>();
        public ICollection<Module> Modules { get; set; } = new List<Module>();
        public ICollection<Tag> Tags { get; set; } = new List<Tag>();
        public ICollection<TestSuite> TestSuites { get; set; } = new List<TestSuite>();
        public ICollection<TestDataSet> TestDataSets { get; set; } = new List<TestDataSet>();
        public ICollection<TestData.Environment> Environments { get; set; } = new List<TestData.Environment>();
    }
}