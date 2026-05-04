using Application.Features.TestSuitesFeature.DTOs;
using Application.Interfaces;
using MediatR;

namespace Application.Features.TestSuitesFeature.Queries
{
    public record GetTestSuitesByProjectIdQuery(Guid ProjectId) : IRequest<List<TestSuiteDTO>>;

    public class GetTestSuitesByProjectIdQueryHandler
        : IRequestHandler<GetTestSuitesByProjectIdQuery, List<TestSuiteDTO>>
    {
        private readonly ITestSuiteRepository _testSuiteRepository;

        public GetTestSuitesByProjectIdQueryHandler(ITestSuiteRepository testSuiteRepository)
        {
            _testSuiteRepository = testSuiteRepository;
        }

        public async Task<List<TestSuiteDTO>> Handle(
            GetTestSuitesByProjectIdQuery request,
            CancellationToken cancellationToken)
        {
            var testSuites = await _testSuiteRepository.GetByProjectIdAsync(request.ProjectId);

            return testSuites.Select(ts => new TestSuiteDTO
            {
                Id = ts.Id,
                ProjectId = ts.ProjectId,
                Name = ts.Name,
                Description = ts.Description,
                CreatedById = ts.CreatedById,
                CreatedDate = ts.CreatedDate,
                ModifiedDate = ts.ModifiedDate,
                ScenarioCount = ts.TestSuiteScenarios != null ? ts.TestSuiteScenarios.Count : 0
            }).ToList();
        }
    }
}
