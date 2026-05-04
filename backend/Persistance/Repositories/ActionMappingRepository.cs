// Repository implementation for ActionMapping.
// Handles database operations for action mappings (NLP intent → UI action links).

using Application.Interfaces;
using Domain.Entities.NLP;
using Microsoft.EntityFrameworkCore;
using Persistance.Data;

namespace Persistance.Repositories
{
    /// <summary>
    /// Concrete repository for ActionMapping.
    /// GenericRepository gives us CRUD. We add project-scoped queries.
    /// </summary>
    public class ActionMappingRepository : GenericRepository<ActionMapping>, IActionMappingRepository
    {
        public ActionMappingRepository(TestAutoumatisationContext context) : base(context)
        {
        }

        /// <summary>
        /// Gets all active (non-deleted) action mappings for a specific project.
        /// Used when the automation engine needs to know how to translate NLP intents to actions.
        /// </summary>
        public async Task<List<ActionMapping>> GetByProjectIdAsync(Guid projectId, CancellationToken cancellationToken)
        {
            return await _context.ActionMappings
                .AsNoTracking()                                    // Read-only for performance
                .Where(am => am.ProjectId == projectId             // Filter by project
                          && am.IsActive                           // Only active mappings
                          && am.IsDeleted == false)                // Exclude soft-deleted
                .OrderByDescending(am => am.Priority)             // Higher priority first
                .ThenBy(am => am.IntentPattern)                    // Alphabetical tie-break
                .ToListAsync(cancellationToken);
        }
    }
}
