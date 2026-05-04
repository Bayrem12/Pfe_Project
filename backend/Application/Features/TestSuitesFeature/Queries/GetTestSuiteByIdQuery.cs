using Application.Features.TestSuitesFeature.DTOs;
using Application.Interfaces;
using MediatR;

namespace Application.Features.TestSuitesFeature.Queries
{
    public record GetTestSuiteByIdQuery(Guid Id) : IRequest<TestSuiteDTO?>;

    public class GetTestSuiteByIdQueryHandler
        : IRequestHandler<GetTestSuiteByIdQuery, TestSuiteDTO?>
    {
        private readonly ITestSuiteRepository _testSuiteRepository;

        public GetTestSuiteByIdQueryHandler(ITestSuiteRepository testSuiteRepository)
        {
            _testSuiteRepository = testSuiteRepository;
        }

        public async Task<TestSuiteDTO?> Handle(
            GetTestSuiteByIdQuery request,
            CancellationToken cancellationToken)
        {
            var testSuite = await _testSuiteRepository.GetByIdAsync(request.Id, cancellationToken);
            if (testSuite == null)
            {
                return null;
            }

            return new TestSuiteDTO
            {
                Id = testSuite.Id,
                ProjectId = testSuite.ProjectId,
                Name = testSuite.Name,
                Description = testSuite.Description,
                CreatedById = testSuite.CreatedById,
                CreatedDate = testSuite.CreatedDate,
                ModifiedDate = testSuite.ModifiedDate
            };
        }
    }
}
