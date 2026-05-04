using Application.Features.TestSuitesFeature.Queries;
using Application.Interfaces;
using Domain.Entities.Scenarios;
using FluentAssertions;
using Moq;
using Xunit;

namespace UnitTest.Features.TestSuitesFeature.Queries
{
    public class GetTestSuitesByProjectIdQueryHandlerTests
    {
        private readonly Mock<ITestSuiteRepository> _mockRepository;
        private readonly GetTestSuitesByProjectIdQueryHandler _handler;

        public GetTestSuitesByProjectIdQueryHandlerTests()
        {
            _mockRepository = new Mock<ITestSuiteRepository>();
            _handler = new GetTestSuitesByProjectIdQueryHandler(_mockRepository.Object);
        }

        [Fact]
        public async Task Handle_ValidProjectId_ReturnsListOfDTOs()
        {
            // Arrange
            var projectId = Guid.NewGuid();
            var testSuites = new List<TestSuite>
            {
                new TestSuite
                {
                    Id = Guid.NewGuid(),
                    ProjectId = projectId,
                    Name = "Suite 1",
                    Description = "Description 1",
                    CreatedById = Guid.NewGuid(),
                    CreatedDate = DateTime.UtcNow
                },
                new TestSuite
                {
                    Id = Guid.NewGuid(),
                    ProjectId = projectId,
                    Name = "Suite 2",
                    Description = "Description 2",
                    CreatedById = Guid.NewGuid(),
                    CreatedDate = DateTime.UtcNow
                }
            };

            _mockRepository.Setup(r => r.GetByProjectIdAsync(projectId))
                .ReturnsAsync(testSuites);

            var query = new GetTestSuitesByProjectIdQuery(projectId);

            // Act
            var result = await _handler.Handle(query, CancellationToken.None);

            // Assert
            result.Should().HaveCount(2);
            result[0].Name.Should().Be("Suite 1");
            result[1].Name.Should().Be("Suite 2");
        }

        [Fact]
        public async Task Handle_NoTestSuites_ReturnsEmptyList()
        {
            // Arrange
            var projectId = Guid.NewGuid();
            _mockRepository.Setup(r => r.GetByProjectIdAsync(projectId))
                .ReturnsAsync(new List<TestSuite>());

            var query = new GetTestSuitesByProjectIdQuery(projectId);

            // Act
            var result = await _handler.Handle(query, CancellationToken.None);

            // Assert
            result.Should().BeEmpty();
        }

        [Fact]
        public async Task Handle_MapsAllPropertiesCorrectly()
        {
            // Arrange
            var projectId = Guid.NewGuid();
            var createdById = Guid.NewGuid();
            var testSuiteId = Guid.NewGuid();
            var createdDate = DateTime.UtcNow;
            var modifiedDate = DateTime.UtcNow.AddHours(1);

            var testSuites = new List<TestSuite>
            {
                new TestSuite
                {
                    Id = testSuiteId,
                    ProjectId = projectId,
                    Name = "Test Suite",
                    Description = "Test Description",
                    CreatedById = createdById,
                    CreatedDate = createdDate,
                    ModifiedDate = modifiedDate
                }
            };

            _mockRepository.Setup(r => r.GetByProjectIdAsync(projectId))
                .ReturnsAsync(testSuites);

            var query = new GetTestSuitesByProjectIdQuery(projectId);

            // Act
            var result = await _handler.Handle(query, CancellationToken.None);

            // Assert
            result.Should().HaveCount(1);
            var dto = result[0];
            dto.Id.Should().Be(testSuiteId);
            dto.ProjectId.Should().Be(projectId);
            dto.Name.Should().Be("Test Suite");
            dto.Description.Should().Be("Test Description");
            dto.CreatedById.Should().Be(createdById);
            dto.CreatedDate.Should().Be(createdDate);
            dto.ModifiedDate.Should().Be(modifiedDate);
        }
    }
}
