// Repository implementation for StepAnalysis.
// This is in the PERSISTANCE (outer) layer — it implements the interface from APPLICATION (inner) layer.
// This is Dependency Inversion: Application defines WHAT, Persistance defines HOW.
//
// It extends GenericRepository<StepAnalysis> which gives us:
// - Post(entity), PostRange(entities), GetById(id), GetAll(), Update(entity), Delete(id), etc.
// We only add CUSTOM methods specific to StepAnalysis.

using Application.Interfaces;
using Domain.Entities.NLP;
using Microsoft.EntityFrameworkCore;
using Persistance.Data;

namespace Persistance.Repositories
{
    /// <summary>
    /// Concrete repository for StepAnalysis with custom query methods.
    /// Inherits all generic CRUD from GenericRepository.
    /// </summary>
    public class StepAnalysisRepository : GenericRepository<StepAnalysis>, IStepAnalysisRepository
    {
        // Constructor: pass the DbContext to the base GenericRepository
        public StepAnalysisRepository(TestAutoumatisationContext context) : base(context)
        {
        }

        /// <summary>
        /// Gets all step analyses for a scenario by loading the steps of that scenario
        /// and finding their analyses. Includes Parameters for the full data tree.
        /// 
        /// The query chain:
        /// 1. Start from StepAnalyses table
        /// 2. Include Parameters (eager loading — loads related StepParameter records)
        /// 3. Where the Step belongs to the given scenario
        /// 4. Filter out soft-deleted records (IsDeleted == false)
        /// 5. Execute as read-only (AsNoTracking = better performance, no change tracking)
        /// </summary>
        public async Task<List<StepAnalysis>> GetByScenarioIdAsync(Guid scenarioId, CancellationToken cancellationToken)
        {
            return await _context.StepAnalyses
                .Include(sa => sa.Parameters)    // Eager load: also fetch the Parameters collection
                .Include(sa => sa.Step)           // Eager load: also fetch the Step entity
                .AsNoTracking()                   // Read-only: better performance (no EF change tracking)
                .Where(sa => sa.Step.ScenarioId == scenarioId  // Filter by scenario
                          && sa.IsDeleted == false)            // Exclude soft-deleted records
                .OrderBy(sa => sa.AnalyzedAt)                  // Order by analysis date
                .ToListAsync(cancellationToken);               // Execute the SQL query
        }
    }
}
