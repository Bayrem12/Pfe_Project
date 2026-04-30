using Application.Features.TestSuitesFeature.Commands;
using Application.Interfaces;
using Domain.Entities.Scenarios;
using FluentAssertions;
using Moq;
using Xunit;

namespace UnitTest.Features.TestSuitesFeature.Commands
{
    public class DeleteTestSuiteCommandHandlerTests
    {
        private readonly Mock<ITestSuiteRepository> _mockRepository;
        private readonly DeleteTestSuiteCommandHandler _handler;

        public DeleteTestSuiteCommandHandlerTests()
        {
            _mockRepository = new Mock<ITestSuiteRepository>();
            _handler = new DeleteTestSuiteCommandHandler(_mockRepository.Object);
        }

        [Fact]
        public async Task Handle_ExistingTestSuite_ReturnsTrue()
        {
            // Arrange
            var testSuiteId = Guid.NewGuid();
            var existingTestSuite = new TestSuite { Id = testSuiteId, Name = "Test Suite" };

            _mockRepository.Setup(r => r.GetByIdAsync(testSuiteId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(existingTestSuite);
            _mockRepository.Setup(r => r.SoftDelete(testSuiteId))
                .ReturnsAsync(true);
            _mockRepository.Setup(r => r.SaveChange(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var command = new DeleteTestSuiteCommand(testSuiteId);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.Should().BeTrue();
            _mockRepository.Verify(r => r.SoftDelete(testSuiteId), Times.Once);
            _mockRepository.Verify(r => r.SaveChange(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Handle_NonExistentTestSuite_ReturnsFalse()
        {
            // Arrange
            var testSuiteId = Guid.NewGuid();
            _mockRepository.Setup(r => r.GetByIdAsync(testSuiteId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((TestSuite?)null);

            var command = new DeleteTestSuiteCommand(testSuiteId);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.Should().BeFalse();
            _mockRepository.Verify(r => r.SoftDelete(It.IsAny<Guid>()), Times.Never);
        }

        [Fact]
        public async Task Handle_SoftDeleteFails_ReturnsFalse()
        {
            // Arrange
            var testSuiteId = Guid.NewGuid();
            var existingTestSuite = new TestSuite { Id = testSuiteId, Name = "Test Suite" };

            _mockRepository.Setup(r => r.GetByIdAsync(testSuiteId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(existingTestSuite);
            _mockRepository.Setup(r => r.SoftDelete(testSuiteId))
                .ReturnsAsync(false);

            var command = new DeleteTestSuiteCommand(testSuiteId);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.Should().BeFalse();
            _mockRepository.Verify(r => r.SaveChange(It.IsAny<CancellationToken>()), Times.Never);
        }
    }
}
