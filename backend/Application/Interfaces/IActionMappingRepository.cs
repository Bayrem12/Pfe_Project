// Repository interface for ActionMapping entity.
// ActionMapping links NLP intent patterns to specific UI automation actions.

using Domain.Entities.NLP;

namespace Application.Interfaces
{
    /// <summary>
    /// Repository for action mapping operations.
    /// Extends IGenericRepository which gives us basic CRUD.
    /// We add project-specific query methods here.
    /// </summary>
    public interface IActionMappingRepository : IGenericRepository<ActionMapping>
    {
        /// <summary>
        /// Get all active action mappings for a specific project.
        /// Used by the automation engine to know what UI actions to perform.
        /// </summary>
        Task<List<ActionMapping>> GetByProjectIdAsync(Guid projectId, CancellationToken cancellationToken);
    }
}
