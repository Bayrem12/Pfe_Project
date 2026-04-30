using Domain.Common;
using Domain.Entities.ProjectManagement;

namespace Domain.Entities.TestData
{
    public class TestDataSet : Entity
    {
        public Guid ProjectId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;

        // Navigation
        public Project Project { get; set; } = null!;
        public ICollection<TestDataEntry> Entries { get; set; } = new List<TestDataEntry>();
    }
}