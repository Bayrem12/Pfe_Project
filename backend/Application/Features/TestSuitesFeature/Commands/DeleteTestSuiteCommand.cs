using Application.Interfaces;
using MediatR;

namespace Application.Features.TestSuitesFeature.Commands
{
    public record DeleteTestSuiteCommand(Guid Id) : IRequest<bool>;

    public class DeleteTestSuiteCommandHandler : IRequestHandler<DeleteTestSuiteCommand, bool>
    {
        private readonly ITestSuiteRepository _testSuiteRepository;

        public DeleteTestSuiteCommandHandler(ITestSuiteRepository testSuiteRepository)
        {
            _testSuiteRepository = testSuiteRepository;
        }

        public async Task<bool> Handle(DeleteTestSuiteCommand request, CancellationToken cancellationToken)
        {
            var testSuite = await _testSuiteRepository.GetByIdAsync(request.Id, cancellationToken);
            if (testSuite == null)
            {
                return false;
            }

            var deleted = await _testSuiteRepository.SoftDelete(request.Id);
            if (!deleted)
            {
                return false;
            }

            await _testSuiteRepository.SaveChange(cancellationToken);
            return true;
        }
    }
}
