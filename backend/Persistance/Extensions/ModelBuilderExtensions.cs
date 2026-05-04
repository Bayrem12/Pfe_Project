using Domain.Entities.Identity;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence
{
    public static class ModelBuilderExtensions
    {
        // Fixed Role IDs for seeding - these are the 4 system roles (matching ProjectRole enum)
        public static readonly Guid AdminRoleId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        public static readonly Guid ManagerRoleId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        public static readonly Guid TesterRoleId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
        public static readonly Guid ViewerRoleId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");

        public static void SeedContext(this ModelBuilder modelBuilder)
        {
            SeedRoles(modelBuilder);
            SeedUsers(modelBuilder);
        }

        private static void SeedRoles(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Role>().HasData(
                new Role
                {
                    Id = AdminRoleId,
                    Name = "Admin",
                    Description = "Admin with full access",
                    CreatedDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    IsDeleted = false
                },
                new Role
                {
                    Id = ManagerRoleId,
                    Name = "Manager",
                    Description = "Manager with elevated permissions",
                    CreatedDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    IsDeleted = false
                },
                new Role
                {
                    Id = TesterRoleId,
                    Name = "Tester",
                    Description = "Tester with test execution permissions",
                    CreatedDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    IsDeleted = false
                },
                new Role
                {
                    Id = ViewerRoleId,
                    Name = "Viewer",
                    Description = "Viewer with read-only access (default role)",
                    CreatedDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    IsDeleted = false
                }
            );
        }

        private static void SeedUsers(ModelBuilder modelBuilder)
        {
            var user1Id = Guid.Parse("11111111-1111-1111-1111-111111111111");
            var user2Id = Guid.Parse("22222222-2222-2222-2222-222222222222");

            modelBuilder.Entity<User>().HasData(
                new User
                {
                    Id = user1Id,
                    FirstName = "Test",
                    LastName = "User",
                    Email = "test@example.com",
                    PasswordHash = "$2b$12$gxGP8ndnJmB8.FjXLt4EOuFgpiiGW.qxGoyYdBuepx.NnqlI4qwNq",
                    IsActive = true,
                    CreatedDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    IsDeleted = false
                },
                new User
                {
                    Id = user2Id,
                    FirstName = "Second",
                    LastName = "User",
                    Email = "second@example.com",
                    PasswordHash = "3ukpEG95GHquxJI3+SWZxpCLUgZGiSPU+Y9PBOXKiuM=",
                    IsActive = true,
                    CreatedDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    IsDeleted = false
                }
            );

            // Assign Viewer role to seeded users
            modelBuilder.Entity<UserRole>().HasData(
                new UserRole
                {
                    Id = Guid.Parse("11111111-aaaa-1111-aaaa-111111111111"),
                    UserId = user1Id,
                    RoleId = ViewerRoleId,
                    CreatedDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    IsDeleted = false
                },
                new UserRole
                {
                    Id = Guid.Parse("22222222-aaaa-2222-aaaa-222222222222"),
                    UserId = user2Id,
                    RoleId = ViewerRoleId,
                    CreatedDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    IsDeleted = false
                }
            );
        }
    }
}
