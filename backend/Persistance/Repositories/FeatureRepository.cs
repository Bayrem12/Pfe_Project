using Application.Interfaces;
using Domain.Entities.ProjectManagement;
using Microsoft.EntityFrameworkCore;
using Persistance.Data;

namespace Persistance.Repositories
{
    public class FeatureRepository : IFeatureRepository
    {
        private readonly TestAutoumatisationContext _context;

        public FeatureRepository(TestAutoumatisationContext context)
        {
            _context = context;
        }

        public async Task<Feature> Post(Feature feature)
        {
            await _context.Features.AddAsync(feature);
            return feature;
        }

        public async Task<Feature> Update(Feature feature)
        {
            _context.Features.Update(feature);
            return await Task.FromResult(feature);
        }

        public async Task SoftDelete(Feature feature)
        {
            feature.IsDeleted = true;
            feature.DeletedDate = DateTime.UtcNow;
            _context.Features.Update(feature);
            await Task.CompletedTask;
        }

        public async Task<Feature?> GetById(Guid id)
        {
            return await _context.Features
                .Include(f => f.Module)
                .Include(f => f.Scenarios)
                .FirstOrDefaultAsync(f => f.Id == id && !f.IsDeleted);
        }

        public async Task<List<Feature>> GetByModuleId(Guid moduleId)
        {
            return await _context.Features
                .Include(f => f.Scenarios)
                .Where(f => f.ModuleId == moduleId && !f.IsDeleted)
                .OrderBy(f => f.DisplayOrder)
                .ToListAsync();
        }

        public async Task<List<Feature>> GetAll()
        {
            return await _context.Features
                .Include(f => f.Module)
                .Include(f => f.Scenarios)
                .Where(f => !f.IsDeleted)
                .OrderBy(f => f.DisplayOrder)
                .ToListAsync();
        }

        public async Task<int> SaveChange(CancellationToken cancellationToken)
        {
            return await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
