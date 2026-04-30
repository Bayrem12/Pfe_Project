using Domain.Common;
using Domain.Entities.ProjectManagement;

namespace Application.Interfaces
{
    public interface IProjectRepository : IGenericRepository<Project>
    {
        Task<PagedList<Project>> GetAllProjectsByUserIdAsync(Guid userId, int? pageNumber, int? pageSize, CancellationToken cancellationToken);
        Task<Project?> GetProjectWithMembersAsync(Guid projectId, CancellationToken cancellationToken);
        Task<List<ProjectMember>> GetProjectMembersAsync(Guid projectId, CancellationToken cancellationToken);
        Task<ProjectMember?> GetProjectMemberAsync(Guid projectId, Guid userId, CancellationToken cancellationToken);
        Task<ProjectMember> AddMemberAsync(ProjectMember member, CancellationToken cancellationToken);
        Task RemoveMemberAsync(Guid projectId, Guid userId, CancellationToken cancellationToken);
    }
}
