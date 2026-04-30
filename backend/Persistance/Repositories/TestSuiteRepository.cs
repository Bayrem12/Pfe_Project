using Application.Interfaces;
using Domain.Entities.Scenarios;
using Microsoft.EntityFrameworkCore;
using Persistance.Data;

namespace Persistance.Repositories
{
    public class TestSuiteRepository : GenericRepository<TestSuite>, ITestSuiteRepository
    {
        public TestSuiteRepository(TestAutoumatisationContext context) : base(context)
        {
        }

        public async Task<TestSuite> AddAsync(TestSuite testSuite)
        {
            await _context.TestSuites.AddAsync(testSuite);
            await _context.SaveChangesAsync();
            return testSuite;
        }

        public async Task<IReadOnlyList<TestSuite>> GetByProjectIdAsync(Guid projectId)
        {
            return await _context.TestSuites
                .AsNoTracking()
                .Include(ts => ts.TestSuiteScenarios.Where(tss => !tss.IsDeleted))
                .Where(ts => ts.ProjectId == projectId && !ts.IsDeleted)
                .OrderBy(ts => ts.Name)
                .ToListAsync();
        }

        public async Task<TestSuite?> GetWithCasesAsync(Guid testSuiteId)
        {
            return await _context.TestSuites
                .AsNoTracking()
                .Include(ts => ts.TestSuiteScenarios.Where(tss => !tss.IsDeleted))
                    .ThenInclude(tss => tss.Scenario)
                .Where(ts => ts.Id == testSuiteId && !ts.IsDeleted)
                .FirstOrDefaultAsync();
        }

        public async Task UpdateAsync(TestSuite testSuite)
        {
            _context.TestSuites.Update(testSuite);
        }
    }
}
