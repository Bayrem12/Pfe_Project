using Application.Interfaces;
using Domain.Common;
using Domain.Entities.ProjectManagement;
using Microsoft.EntityFrameworkCore;
using Persistance.Data;

namespace Persistance.Repositories
{
    public class ProjectRepository : GenericRepository<Project>, IProjectRepository
    {
        public ProjectRepository(TestAutoumatisationContext context) : base(context)
        {
        }

        public async Task<PagedList<Project>> GetAllProjectsByUserIdAsync(Guid userId, int? pageNumber, int? pageSize, CancellationToken cancellationToken)
        {
            var userIdString = userId.ToString();

            var globalRole = await _context.Users
                .AsNoTracking()
                .Where(u => u.Id == userId)
                .SelectMany(u => u.UserRoles)
                .Select(ur => ur.Role.Name)
                .FirstOrDefaultAsync(cancellationToken);

            var normalizedRole = (globalRole ?? string.Empty).Trim().ToLowerInvariant();
            var isOwner = normalizedRole == "owner";
            var isTester = normalizedRole == "tester";

            IQueryable<Project> query = _context.Projects
                .AsNoTracking()
                .Where(p => !p.IsDeleted);

            if (!isOwner)
            {
                if (isTester)
                {
                    // Tester: projets créés + projets où il est membre
                    query = query.Where(p =>
                        p.UserId == userId ||
                        p.CreatedById == userIdString ||
                        p.Members.Any(m => !m.IsDeleted && m.UserId == userId));
                }
                else
                {
                    // Viewer / Manager: uniquement les projets où l'utilisateur est membre
                    query = query.Where(p => p.Members.Any(m => !m.IsDeleted && m.UserId == userId));
                }
            }

            query = query
                .Include(p => p.Members)
                    .ThenInclude(m => m.User)
                        .ThenInclude(u => u.UserRoles)
                            .ThenInclude(ur => ur.Role)
                .OrderByDescending(p => p.ModifiedDate ?? p.CreatedDate);

            int totalRows = await query.CountAsync(cancellationToken);

            var page = pageNumber.GetValueOrDefault(1);
            if (page <= 0) page = 1;

            var size = pageSize.GetValueOrDefault(int.MaxValue);
            if (size <= 0) size = int.MaxValue;

            var projects = await query
                .Skip((page - 1) * size)
                .Take(size)
                .ToListAsync(cancellationToken);

            return new PagedList<Project>(projects, totalRows, pageNumber, pageSize);
        }

        public async Task<Project?> GetProjectWithMembersAsync(Guid projectId, CancellationToken cancellationToken)
        {
            return await _context.Projects
                .AsNoTracking()
                .Include(p => p.Members)
                    .ThenInclude(m => m.User)
                        .ThenInclude(u => u.UserRoles)
                            .ThenInclude(ur => ur.Role)
                .FirstOrDefaultAsync(p => p.Id == projectId && !p.IsDeleted, cancellationToken);
        }

        public async Task<List<ProjectMember>> GetProjectMembersAsync(Guid projectId, CancellationToken cancellationToken)
        {
            return await _context.ProjectMembers
                .AsNoTracking()
                .Include(m => m.User)
                    .ThenInclude(u => u.UserRoles)
                        .ThenInclude(ur => ur.Role)
                .Where(m => m.ProjectId == projectId && !m.IsDeleted)
                .ToListAsync(cancellationToken);
        }

        public async Task<ProjectMember?> GetProjectMemberAsync(Guid projectId, Guid userId, CancellationToken cancellationToken)
        {
            return await _context.ProjectMembers
                .FirstOrDefaultAsync(m => m.ProjectId == projectId && m.UserId == userId && !m.IsDeleted, cancellationToken);
        }

        public async Task<ProjectMember> AddMemberAsync(ProjectMember member, CancellationToken cancellationToken)
        {
            await _context.ProjectMembers.AddAsync(member, cancellationToken);
            return member;
        }

        public async Task RemoveMemberAsync(Guid projectId, Guid userId, CancellationToken cancellationToken)
        {
            var member = await _context.ProjectMembers
                .FirstOrDefaultAsync(m => m.ProjectId == projectId && m.UserId == userId && !m.IsDeleted, cancellationToken);
            
            if (member != null)
            {
                _context.ProjectMembers.Remove(member);
            }
        }
    }
}
