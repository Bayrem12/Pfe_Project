// Repository interface for StepAnalysis entity.
// In Clean Architecture, interfaces live in the Application layer (inner layer).
// The actual implementation lives in the Persistance layer (outer layer).
// This is called "Dependency Inversion" — the inner layer defines WHAT it needs,
// the outer layer decides HOW to provide it.

using Domain.Entities.NLP;

namespace Application.Interfaces
{
    /// <summary>
    /// Repository for NLP step analysis operations.
    /// Extends IGenericRepository which already provides: Post, GetById, GetAll, Update, Delete, etc.
    /// We only add methods that are SPECIFIC to StepAnalysis here.
    /// </summary>
    public interface IStepAnalysisRepository : IGenericRepository<StepAnalysis>
    {
        /// <summary>
        /// Get all step analyses for a specific scenario (by loading each step's analysis).
        /// Includes the Parameters navigation property so we get the full data.
        /// </summary>
        Task<List<StepAnalysis>> GetByScenarioIdAsync(Guid scenarioId, CancellationToken cancellationToken);
    }
}
