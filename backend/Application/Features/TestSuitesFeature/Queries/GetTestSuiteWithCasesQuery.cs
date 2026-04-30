using Application.Features.TestSuitesFeature.DTOs;
using Application.Interfaces;
using MediatR;

namespace Application.Features.TestSuitesFeature.Queries
{
    public record GetTestSuiteWithCasesQuery(Guid Id) : IRequest<TestSuiteWithCasesDTO?>;

    public class GetTestSuiteWithCasesQueryHandler
        : IRequestHandler<GetTestSuiteWithCasesQuery, TestSuiteWithCasesDTO?>
    {
        private readonly ITestSuiteRepository _testSuiteRepository;

        public GetTestSuiteWithCasesQueryHandler(ITestSuiteRepository testSuiteRepository)
        {
            _testSuiteRepository = testSuiteRepository;
        }

        public async Task<TestSuiteWithCasesDTO?> Handle(
            GetTestSuiteWithCasesQuery request,
            CancellationToken cancellationToken)
        {
            var testSuite = await _testSuiteRepository.GetWithCasesAsync(request.Id);
            if (testSuite == null)
            {
                return null;
            }

            return new TestSuiteWithCasesDTO
            {
                Id = testSuite.Id,
                ProjectId = testSuite.ProjectId,
                Name = testSuite.Name,
                Description = testSuite.Description,
                CreatedById = testSuite.CreatedById,
                CreatedDate = testSuite.CreatedDate,
                ModifiedDate = testSuite.ModifiedDate,
                Scenarios = testSuite.TestSuiteScenarios
                    .OrderBy(tss => tss.DisplayOrder)
                    .Select(tss => new TestSuiteScenarioDTO
                    {
                        Id = tss.Id,
                        ScenarioId = tss.ScenarioId,
                        ScenarioTitle = tss.Scenario?.Title ?? string.Empty,
                        ScenarioDescription = tss.Scenario?.Description ?? string.Empty,
                        DisplayOrder = tss.DisplayOrder
                    }).ToList()
            };
        }
    }
}
