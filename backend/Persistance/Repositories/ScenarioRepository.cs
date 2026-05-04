using Application.Interfaces;
using Domain.Entities.Scenarios;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Persistance.Data;

namespace Persistance.Repositories
{
    public class ScenarioRepository : GenericRepository<Scenario>, IScenarioRepository
    {
        public ScenarioRepository(TestAutoumatisationContext context) : base(context)
        {
        }

        public IQueryable<Scenario> GetQueryable()
        {
            return _context.Scenarios
                .AsQueryable()
                .AsNoTracking();
        }

        public async Task<Scenario?> GetWithStepsAsync(Guid scenarioId, CancellationToken cancellationToken)
        {
            return await _context.Scenarios
                .AsNoTracking()
                .Include(s => s.Steps.Where(step => !step.IsDeleted).OrderBy(step => step.DisplayOrder))
                .FirstOrDefaultAsync(s => s.Id == scenarioId && !s.IsDeleted, cancellationToken);
        }
        public async Task<Tag> GetOrCreateTagAsync(string tagName, Guid projectId, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(tagName))
                throw new ArgumentException("Tag name cannot be empty");

            var normalized = tagName.Trim().ToLower();

            var existingTag = await _context.Tags
                .FirstOrDefaultAsync(t => t.Name.ToLower() == normalized && t.ProjectId == projectId, ct);

            if (existingTag != null)
                return existingTag;

            var newTag = new Tag
            {
                Id = Guid.NewGuid(),
                Name = tagName.Trim(),
                ProjectId = projectId
            };

            await _context.Tags.AddAsync(newTag, ct);

            return newTag;
        }

        public async Task<IReadOnlyList<Scenario>> GetByFeatureIdAsync(Guid featureId, CancellationToken ct = default)
        {
            return await _context.Scenarios
                .AsNoTracking()
                .Where(s => s.FeatureId == featureId && !s.IsDeleted)
                .ToListAsync(ct);
        }

        public async Task<IReadOnlyList<Scenario>> GetByProjectIdAsync(Guid projectId, CancellationToken ct = default)
        {
            return await _context.Scenarios
                .AsNoTracking()
                .Include(s => s.Feature)
                    .ThenInclude(f => f.Module)
                .Where(s => s.Feature.Module.ProjectId == projectId && !s.IsDeleted)
                .ToListAsync(ct);
        }

        public async Task<IReadOnlyList<Scenario>> SearchAsync(Guid projectId, string? searchTerm, ScenarioStatus? status, CancellationToken ct = default)
        {
            var query = _context.Scenarios
                .AsNoTracking()
                .Include(s => s.Feature)
                    .ThenInclude(f => f.Module)
                .Where(s => s.Feature.Module.ProjectId == projectId && !s.IsDeleted);

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var searchLower = searchTerm.ToLower();

                query = query.Where(s =>
                    s.Title.ToLower().Contains(searchLower) ||
                    s.Description.ToLower().Contains(searchLower));
            }

            if (status.HasValue)
            {
                query = query.Where(s => s.Status == status.Value);
            }

            return await query.ToListAsync(ct);
        }

        public async Task<Scenario?> GetWithVersionsAsync(Guid id, CancellationToken ct = default)
        {
            return await _context.Scenarios
                .AsNoTracking()
                .Include(s => s.Versions)
                .FirstOrDefaultAsync(s => s.Id == id && !s.IsDeleted, ct);
        }

        public async Task<Scenario?> GetFullAsync(Guid id, CancellationToken ct = default)
        {
            return await _context.Scenarios
                .AsNoTracking()
                .Include(s => s.Steps.Where(step => !step.IsDeleted))
                .Include(s => s.Versions.Where(v => !v.IsDeleted))
                .Include(s => s.ScenarioTags.Where(st => !st.IsDeleted))
                    .ThenInclude(st => st.Tag)
                .Include(s => s.Feature)
                    .ThenInclude(f => f.Module)
                        .ThenInclude(m => m.Project)
                .Include(s => s.TestResults)
                .FirstOrDefaultAsync(s => s.Id == id && !s.IsDeleted, ct);
        }

        public async Task<Scenario?> GetForUpdateAsync(Guid id, CancellationToken ct = default)
        {
            return await _context.Scenarios
                .Include(s => s.Steps.Where(step => !step.IsDeleted))
                .Include(s => s.Feature)
                    .ThenInclude(f => f.Module)
                .FirstOrDefaultAsync(s => s.Id == id && !s.IsDeleted, ct);
        }
        public async Task<IReadOnlyList<Scenario>> GetAllByIdsAsync(IEnumerable<Guid> ids, CancellationToken ct = default)
        {
            return await _context.Scenarios
                .Where(s => ids.Contains(s.Id) && !s.IsDeleted)
                .ToListAsync(ct);
        }
        public async Task<Scenario?> GetByIdWithIncludes(
    Guid id,
    Func<IQueryable<Scenario>, IQueryable<Scenario>> include,
    CancellationToken cancellationToken)
        {
            IQueryable<Scenario> query = _context.Scenarios;

            query = include(query);

            return await query
                .FirstOrDefaultAsync(s => s.Id == id && !s.IsDeleted, cancellationToken);
        }


    }
}