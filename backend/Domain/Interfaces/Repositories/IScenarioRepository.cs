using Domain.Entities.Scenarios;
using Domain.Enums;

namespace CleanArchitectur.Domain.Interfaces.Repositories;

public interface IScenarioRepository : IGenericRepository<Scenario>
{
    Task<IReadOnlyList<Scenario>> GetByFeatureIdAsync(Guid featureId, CancellationToken ct = default);
    Task<IReadOnlyList<Scenario>> GetByProjectIdAsync(Guid projectId, CancellationToken ct = default);
    Task<IReadOnlyList<Scenario>> SearchAsync(Guid projectId, string? searchTerm, ScenarioStatus? status, CancellationToken ct = default);
    Task<Scenario?> GetWithStepsAsync(Guid id, CancellationToken ct = default);
    Task<Scenario?> GetWithVersionsAsync(Guid id, CancellationToken ct = default);
    Task<Scenario?> GetFullAsync(Guid id, CancellationToken ct = default);
}
