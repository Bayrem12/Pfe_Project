using Application.Interfaces.Identity;
using Domain.Entities.Identity;
using Microsoft.EntityFrameworkCore;
using Persistance.Data;

namespace Persistance.Repositories.Identity
{
    public class UserRepository : GenericRepository<User>, IUserRepository
    {
        private readonly TestAutoumatisationContext _context;

        public UserRepository(TestAutoumatisationContext context) : base(context)
        {
            _context = context;
        }

        // ---------------- AUTH ----------------
        public async Task<User> CreateAsync(User user)
        {
            await _context.Users.AddAsync(user);
            await _context.SaveChangesAsync();
            return user;
        }

        public async Task<User> CreateWithDefaultRoleAsync(User user)
        {
            await _context.Users.AddAsync(user);
            var viewerRole = await _context.Roles.FirstOrDefaultAsync(r => r.Name == "Viewer");
            if (viewerRole != null)
            {
                var userRole = new UserRole
                {
                    Id = Guid.NewGuid(),
                    UserId = user.Id,
                    RoleId = viewerRole.Id
                };
                await _context.UserRoles.AddAsync(userRole);
                user.UserRoles.Add(userRole);
            }
            await _context.SaveChangesAsync();
            return await _context.Users
                .Include(u => u.UserRoles)
                    .ThenInclude(ur => ur.Role)
                .FirstAsync(u => u.Id == user.Id);
        }

        public async Task<bool> ExistsAsync(string email)
        {
            if (string.IsNullOrWhiteSpace(email)) return false;
            var normalized = email.ToLowerInvariant();
            return await _context.Users.AnyAsync(u => u.Email != null && u.Email.ToLower() == normalized);
        }

        public async Task<User?> GetByEmailAsync(string email)
        {
            if (string.IsNullOrWhiteSpace(email)) return null;
            var normalized = email.ToLowerInvariant();
            return await _context.Users
                .Include(u => u.UserRoles)
                    .ThenInclude(ur => ur.Role)
                .FirstOrDefaultAsync(u => u.Email != null && u.Email.ToLower() == normalized);
        }

        public async Task UpdateAsync(User user)
        {
            _context.Users.Update(user);
            await _context.SaveChangesAsync();
        }


        // ---------------- USER OPERATIONS ----------------
        public async Task<List<User>> GetAllUsersAsync()
        {
            return await _context.Users
                .Include(u => u.UserRoles)
                    .ThenInclude(ur => ur.Role)
                .ToListAsync();
        }

        public async Task<User?> GetUserByIdAsync(Guid id)
        {
            return await _context.Users
                .Include(u => u.UserRoles)
                    .ThenInclude(ur => ur.Role)
                .FirstOrDefaultAsync(u => u.Id == id);
        }

        public async Task<List<User>> SearchUsersAsync(string keyword)
        {
            if (string.IsNullOrWhiteSpace(keyword))
                return await GetAllUsersAsync();

            keyword = keyword.ToLower();
            return await _context.Users
                .Include(u => u.UserRoles)
                    .ThenInclude(ur => ur.Role)
                .Where(u =>
                    (u.FirstName != null && u.FirstName.ToLower().Contains(keyword)) ||
                    (u.LastName != null && u.LastName.ToLower().Contains(keyword)) ||
                    (u.Email != null && u.Email.ToLower().Contains(keyword)))
                .ToListAsync();
        }

        public async Task UpdateUserAsync(User user)
        {
            _context.Users.Update(user);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateUserRolesAsync(Guid id, List<string> roles)
        {
            var user = await _context.Users
                .Include(u => u.UserRoles)
                .FirstOrDefaultAsync(u => u.Id == id);
            if (user != null)
            {
                _context.UserRoles.RemoveRange(user.UserRoles);
                foreach (var roleName in roles)
                {
                    var role = await _context.Roles.FirstOrDefaultAsync(r => r.Name == roleName);
                    if (role != null)
                    {
                        user.UserRoles.Add(new UserRole
                        {
                            UserId = user.Id,
                            RoleId = role.Id
                        });
                    }
                }
                await _context.SaveChangesAsync();
            }
        }

        public async Task ToggleUserStatusAsync(Guid id)
        {
            var user = await GetUserByIdAsync(id);
            if (user != null)
            {
                user.IsActive = !user.IsActive;
                await _context.SaveChangesAsync();
            }
        }

        public async Task<User?> GetByVerificationTokenAsync(string token)
        {
            return await _context.Users
                .FirstOrDefaultAsync(u => u.EmailVerificationToken == token);
        }
    }
}