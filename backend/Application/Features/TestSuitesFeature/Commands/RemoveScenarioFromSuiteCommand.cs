using Application.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.TestSuitesFeature.Commands
{
    public record RemoveScenarioFromSuiteCommand(Guid SuiteId, Guid ScenarioId) : IRequest<bool>;

    public class RemoveScenarioFromSuiteCommandHandler : IRequestHandler<RemoveScenarioFromSuiteCommand, bool>
    {
        private readonly ITestTestAutoumatisationContext _context;

        public RemoveScenarioFromSuiteCommandHandler(ITestTestAutoumatisationContext context)
        {
            _context = context;
        }

        public async Task<bool> Handle(RemoveScenarioFromSuiteCommand request, CancellationToken cancellationToken)
        {
            var testSuiteScenario = await _context.TestSuiteScenarios
                .FirstOrDefaultAsync(tss => tss.TestSuiteId == request.SuiteId
                                         && tss.ScenarioId == request.ScenarioId
                                         && !tss.IsDeleted, cancellationToken);

            if (testSuiteScenario == null)
            {
                return false;
            }

            // Soft delete
            testSuiteScenario.IsDeleted = true;
            testSuiteScenario.DeletedDate = DateTimeOffset.UtcNow;

            await _context.SaveChangesAsync(cancellationToken);
            return true;
        }
    }
}
