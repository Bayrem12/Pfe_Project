using Domain.Common;

namespace Domain.Entities.Identity
{
    public class Role : Entity
    {
        public string Name { get; set; } = default!;
        public string Description { get; set; } = default!;

        public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();

    }
}