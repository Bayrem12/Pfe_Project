using Application.Interfaces;
using Domain.Entities.Scenarios;

namespace Application.Interfaces
{
    public interface ITestSuiteRepository : IGenericRepository<TestSuite>
    {
        Task<TestSuite> AddAsync(TestSuite testSuite);
        Task<IReadOnlyList<TestSuite>> GetByProjectIdAsync(Guid projectId);
        Task<TestSuite?> GetWithCasesAsync(Guid testSuiteId);
        Task UpdateAsync(TestSuite testSuite);
    }
}
