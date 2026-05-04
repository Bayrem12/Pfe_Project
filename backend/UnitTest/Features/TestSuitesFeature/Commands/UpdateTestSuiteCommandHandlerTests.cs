using Application.Features.TestSuitesFeature.Commands;
using Application.Interfaces;
using Domain.Entities.Scenarios;
using FluentAssertions;
using Moq;
using Xunit;

namespace UnitTest.Features.TestSuitesFeature.Commands
{
    public class UpdateTestSuiteCommandHandlerTests
    {
        private readonly Mock<ITestSuiteRepository> _mockRepository;
        private readonly UpdateTestSuiteCommandHandler _handler;

        public UpdateTestSuiteCommandHandlerTests()
        {
            _mockRepository = new Mock<ITestSuiteRepository>();
            _handler = new UpdateTestSuiteCommandHandler(_mockRepository.Object);
        }

        [Fact]
        public async Task Handle_ExistingTestSuite_ReturnsTrue()
        {
            // Arrange
            var testSuiteId = Guid.NewGuid();
            var existingTestSuite = new TestSuite
            {
                Id = testSuiteId,
                Name = "Old Name",
                Description = "Old Description"
            };

            _mockRepository.Setup(r => r.GetByIdAsync(testSuiteId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(existingTestSuite);
            _mockRepository.Setup(r => r.UpdateAsync(It.IsAny<TestSuite>()))
                .Returns(Task.CompletedTask);
            _mockRepository.Setup(r => r.SaveChange(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var command = new UpdateTestSuiteCommand(testSuiteId, "New Name", "New Description");

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.Should().BeTrue();
            _mockRepository.Verify(r => r.UpdateAsync(It.Is<TestSuite>(ts =>
                ts.Name == "New Name" && ts.Description == "New Description")), Times.Once);
            _mockRepository.Verify(r => r.SaveChange(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Handle_NonExistentTestSuite_ReturnsFalse()
        {
            // Arrange
            var testSuiteId = Guid.NewGuid();
            _mockRepository.Setup(r => r.GetByIdAsync(testSuiteId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((TestSuite?)null);

            var command = new UpdateTestSuiteCommand(testSuiteId, "New Name", "New Description");

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.Should().BeFalse();
            _mockRepository.Verify(r => r.UpdateAsync(It.IsAny<TestSuite>()), Times.Never);
        }

        [Fact]
        public async Task Handle_TrimsNameBeforeSaving()
        {
            // Arrange
            var testSuiteId = Guid.NewGuid();
            var existingTestSuite = new TestSuite { Id = testSuiteId, Name = "Old Name", Description = "Old" };

            _mockRepository.Setup(r => r.GetByIdAsync(testSuiteId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(existingTestSuite);
            _mockRepository.Setup(r => r.UpdateAsync(It.IsAny<TestSuite>()))
                .Returns(Task.CompletedTask);
            _mockRepository.Setup(r => r.SaveChange(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var command = new UpdateTestSuiteCommand(testSuiteId, "  Trimmed Name  ", "Description");

            // Act
            await _handler.Handle(command, CancellationToken.None);

            // Assert
            _mockRepository.Verify(r => r.UpdateAsync(It.Is<TestSuite>(ts =>
                ts.Name == "Trimmed Name")), Times.Once);
        }

        [Fact]
        public async Task Handle_SetsModifiedDate()
        {
            // Arrange
            var testSuiteId = Guid.NewGuid();
            var existingTestSuite = new TestSuite
            {
                Id = testSuiteId,
                Name = "Old Name",
                Description = "Old",
                ModifiedDate = null
            };

            _mockRepository.Setup(r => r.GetByIdAsync(testSuiteId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(existingTestSuite);
            _mockRepository.Setup(r => r.UpdateAsync(It.IsAny<TestSuite>()))
                .Returns(Task.CompletedTask);
            _mockRepository.Setup(r => r.SaveChange(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var command = new UpdateTestSuiteCommand(testSuiteId, "New Name", "New Description");

            // Act
            await _handler.Handle(command, CancellationToken.None);

            // Assert
            _mockRepository.Verify(r => r.UpdateAsync(It.Is<TestSuite>(ts =>
                ts.ModifiedDate != null)), Times.Once);
        }
    }
}
