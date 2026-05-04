using Application.Interfaces;
using Domain.Entities.Scenarios;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.TestSuitesFeature.Commands
{
    public record AddScenarioToSuiteCommand(Guid SuiteId, Guid ScenarioId) : IRequest<bool>;

    public class AddScenarioToSuiteCommandHandler : IRequestHandler<AddScenarioToSuiteCommand, bool>
    {
        private readonly ITestTestAutoumatisationContext _context;

        public AddScenarioToSuiteCommandHandler(ITestTestAutoumatisationContext context)
        {
            _context = context;
        }

        public async Task<bool> Handle(AddScenarioToSuiteCommand request, CancellationToken cancellationToken)
        {
            // Verify test suite exists
            var suiteExists = await _context.TestSuites
                .AnyAsync(ts => ts.Id == request.SuiteId && !ts.IsDeleted, cancellationToken);
            if (!suiteExists)
            {
                return false;
            }

            // Verify scenario exists
            var scenarioExists = await _context.Scenarios
                .AnyAsync(s => s.Id == request.ScenarioId && !s.IsDeleted, cancellationToken);
            if (!scenarioExists)
            {
                return false;
            }

            // Check if already linked
            var alreadyLinked = await _context.TestSuiteScenarios
                .AnyAsync(tss => tss.TestSuiteId == request.SuiteId
                              && tss.ScenarioId == request.ScenarioId
                              && !tss.IsDeleted, cancellationToken);
            if (alreadyLinked)
            {
                return true; // Already exists, consider success
            }

            // Get max display order
            var maxOrder = await _context.TestSuiteScenarios
                .Where(tss => tss.TestSuiteId == request.SuiteId && !tss.IsDeleted)
                .Select(tss => (int?)tss.DisplayOrder)
                .MaxAsync(cancellationToken) ?? 0;

            var testSuiteScenario = new TestSuiteScenario
            {
                Id = Guid.NewGuid(),
                TestSuiteId = request.SuiteId,
                ScenarioId = request.ScenarioId,
                DisplayOrder = maxOrder + 1,
                CreatedDate = DateTime.UtcNow
            };

            await _context.TestSuiteScenarios.AddAsync(testSuiteScenario, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
            return true;
        }
    }
}
