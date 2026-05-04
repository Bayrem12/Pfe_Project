// Repository interface for Scenario entity.
// We need this to load scenarios and their steps for the NLP analyze endpoint.

using Domain.Entities.Scenarios;
using Domain.Enums;

namespace Application.Interfaces
{
    /// <summary>
    /// Repository for scenario operations.
    /// Needed by the NLP feature to load a scenario's steps before analyzing them.
    /// </summary>
    public interface IScenarioRepository : IGenericRepository<Scenario>
    {
        /// <summary>
        /// Get a scenario with all its steps loaded (eager loading).
        /// Steps are needed because the NLP analyzes each step's text.
        /// </summary>
        Task<Scenario?> GetWithStepsAsync(Guid scenarioId, CancellationToken cancellationToken);
        Task<IReadOnlyList<Scenario>> GetByFeatureIdAsync(Guid featureId, CancellationToken ct = default);
        Task<IReadOnlyList<Scenario>> GetByProjectIdAsync(Guid projectId, CancellationToken ct = default);
        Task<IReadOnlyList<Scenario>> SearchAsync(Guid projectId, string? searchTerm, ScenarioStatus? status, CancellationToken ct = default);
        Task<Tag> GetOrCreateTagAsync(string tagName, Guid projectId, CancellationToken ct);
        Task<Scenario?> GetWithVersionsAsync(Guid id, CancellationToken ct = default);
        Task<Scenario?> GetFullAsync(Guid id, CancellationToken ct = default);
        Task<Scenario?> GetForUpdateAsync(Guid id, CancellationToken ct = default);
        Task<IReadOnlyList<Scenario>> GetAllByIdsAsync(IEnumerable<Guid> ids, CancellationToken ct = default);
        IQueryable<Scenario> GetQueryable();
        Task<Scenario?> GetByIdWithIncludes(
            Guid id,
            Func<IQueryable<Scenario>, IQueryable<Scenario>> include,
            CancellationToken cancellationToken);
    }
}
