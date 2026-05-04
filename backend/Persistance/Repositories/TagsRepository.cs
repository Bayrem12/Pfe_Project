using Application.Interfaces.Repositories;
using Domain.Entities.Scenarios;
using Microsoft.EntityFrameworkCore;
using Persistance.Data;

namespace Persistance.Repositories
{
    /// <summary>
    /// Implémentation concrète du repository des tags.
    /// </summary>
    public class TagsRepository : GenericRepository<Tag>, ITagsRepository
    {
        public TagsRepository(TestAutoumatisationContext context) : base(context)
        {
        }

        public async Task<Tag> AddAsync(Tag tag)
        {
            await _context.Tags.AddAsync(tag);
            await _context.SaveChangesAsync();
            return tag;
        }

        public async Task<IReadOnlyList<Tag>> GetByProjectIdAsync(Guid projectId)
        {
            return await _context.Tags
                .AsNoTracking()
                .Where(t => t.ProjectId == projectId && !t.IsDeleted)
                .OrderBy(t => t.Name)
                .ToListAsync();
        }
    }
}
