using Domain.Common;

namespace Domain.Entities.Identity
{
    public class UserRole : Entity
    {
        public Guid UserId { get; set; }
        public User User { get; set; } = null!;

        public Guid RoleId { get; set; }
        public Role Role { get; set; } = null!;
    }
}