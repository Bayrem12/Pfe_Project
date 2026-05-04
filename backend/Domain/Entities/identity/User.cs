using Domain.Common;

namespace Domain.Entities.Identity
{
    public class User : Entity
    {
        public string FirstName { get; set; } = default!;
        public string LastName { get; set; } = default!;
        public string Email { get; set; } = default!;
        public string PasswordHash { get; set; } = default!;
        public bool IsActive { get; set; }

        public string FullName => $"{FirstName} {LastName}";

        public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();

        public string? RefreshToken { get; set; }
        public DateTime RefreshTokenExpiryTime { get; set; }

        // 👇 Ajout pour forgot-password
        public string? PasswordResetToken { get; set; }
        public DateTime? PasswordResetTokenExpiry { get; set; }

        // 👇 Email verification
        public string? EmailVerificationToken { get; set; }
        public DateTime? EmailVerificationTokenExpiry { get; set; }
        public DateTime? EmailVerifiedAt { get; set; }
    }
}