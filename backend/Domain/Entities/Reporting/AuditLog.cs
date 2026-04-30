using Domain.Common;
using Domain.Entities.Identity;

namespace Domain.Entities.Reporting
{
    public class AuditLog : Entity
    {
        public Guid UserId { get; set; }
        public string Action { get; set; } = default!;
        public string EntityType { get; set; } = default!;
        public Guid? EntityId { get; set; }
        public string? OldValues { get; set; }
        public string? NewValues { get; set; }
        public DateTime Timestamp { get; set; }
        public string IpAddress { get; set; } = default!;
        public User User { get; set; } = null!;
    }
}