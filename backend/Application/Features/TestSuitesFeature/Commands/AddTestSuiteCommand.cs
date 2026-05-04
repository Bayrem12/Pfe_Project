using Application.Interfaces;
using Domain.Entities.Scenarios;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.TestSuitesFeature.Commands
{
    public record AddTestSuiteCommand(
        Guid ProjectId,
        string Name,
        string Description,
        Guid CreatedById
    ) : IRequest<Guid>;

    public class AddTestSuiteCommandHandler : IRequestHandler<AddTestSuiteCommand, Guid>
    {
        private readonly ITestSuiteRepository _testSuiteRepository;
        private readonly ITestTestAutoumatisationContext _context;

        public AddTestSuiteCommandHandler(
            ITestSuiteRepository testSuiteRepository,
            ITestTestAutoumatisationContext context)
        {
            _testSuiteRepository = testSuiteRepository;
            _context = context;
        }

        public async Task<Guid> Handle(AddTestSuiteCommand request, CancellationToken cancellationToken)
        {
            var projectExists = await _context.Projects
                .AnyAsync(p => p.Id == request.ProjectId, cancellationToken);
            if (!projectExists)
            {
                throw new InvalidOperationException(
                    $"Project with id '{request.ProjectId}' does not exist.");
            }

            var normalizedName = request.Name.Trim();

            var duplicateExists = await _context.TestSuites.AnyAsync(
                ts => ts.ProjectId == request.ProjectId
                      && !ts.IsDeleted
                      && ts.Name.ToLower() == normalizedName.ToLower(),
                cancellationToken);

            if (duplicateExists)
            {
                throw new InvalidOperationException(
                    $"Test suite '{normalizedName}' already exists for this project.");
            }

            var testSuite = new TestSuite
            {
                Id = Guid.NewGuid(),
                ProjectId = request.ProjectId,
                Name = normalizedName,
                Description = request.Description,
                CreatedById = request.CreatedById,
                CreatedDate = DateTime.UtcNow
            };

            var result = await _testSuiteRepository.AddAsync(testSuite);
            return result.Id;
        }
    }
}
