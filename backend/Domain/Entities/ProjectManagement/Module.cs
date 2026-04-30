using Domain.Common;

namespace Domain.Entities.ProjectManagement
{
    public class Module : Entity
    {
        public Guid ProjectId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int DisplayOrder { get; set; }

        // Navigation
        public Project Project { get; set; } = null!;
        public ICollection<Feature> Features { get; set; } = new List<Feature>();
    }

}