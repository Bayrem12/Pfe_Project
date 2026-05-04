using Application.Interfaces;
using MediatR;

namespace Application.Features.TestSuitesFeature.Commands
{
    public record UpdateTestSuiteCommand(
        Guid Id,
        string Name,
        string Description
    ) : IRequest<bool>;

    public class UpdateTestSuiteCommandHandler : IRequestHandler<UpdateTestSuiteCommand, bool>
    {
        private readonly ITestSuiteRepository _testSuiteRepository;

        public UpdateTestSuiteCommandHandler(ITestSuiteRepository testSuiteRepository)
        {
            _testSuiteRepository = testSuiteRepository;
        }

        public async Task<bool> Handle(UpdateTestSuiteCommand request, CancellationToken cancellationToken)
        {
            var testSuite = await _testSuiteRepository.GetByIdAsync(request.Id, cancellationToken);
            if (testSuite == null)
            {
                return false;
            }

            testSuite.Name = request.Name.Trim();
            testSuite.Description = request.Description;
            testSuite.ModifiedDate = DateTime.UtcNow;

            await _testSuiteRepository.UpdateAsync(testSuite);
            await _testSuiteRepository.SaveChange(cancellationToken);
            return true;
        }
    }
}
