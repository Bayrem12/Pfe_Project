using Domain.Common;
using Domain.Entities.Identity;
using Domain.Enums;

namespace Domain.Entities.ProjectManagement
{
    public class ProjectMember : Entity
    {
        public Guid ProjectId { get; set; }
        public Guid UserId { get; set; }
        public ProjectRole Role { get; set; } = ProjectRole.Tester;
        public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public Project Project { get; set; } = null!;
        public User User { get; set; } = null!;
    }
}