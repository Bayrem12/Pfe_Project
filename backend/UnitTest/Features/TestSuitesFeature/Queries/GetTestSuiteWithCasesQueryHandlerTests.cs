using Application.Features.TestSuitesFeature.Queries;
using Application.Interfaces;
using Domain.Entities.Scenarios;
using FluentAssertions;
using Moq;
using Xunit;

namespace UnitTest.Features.TestSuitesFeature.Queries
{
    public class GetTestSuiteWithCasesQueryHandlerTests
    {
        private readonly Mock<ITestSuiteRepository> _mockRepository;
        private readonly GetTestSuiteWithCasesQueryHandler _handler;

        public GetTestSuiteWithCasesQueryHandlerTests()
        {
            _mockRepository = new Mock<ITestSuiteRepository>();
            _handler = new GetTestSuiteWithCasesQueryHandler(_mockRepository.Object);
        }

        [Fact]
        public async Task Handle_ExistingTestSuite_ReturnsDTOWithScenarios()
        {
            // Arrange
            var testSuiteId = Guid.NewGuid();
            var scenarioId1 = Guid.NewGuid();
            var scenarioId2 = Guid.NewGuid();

            var testSuite = new TestSuite
            {
                Id = testSuiteId,
                ProjectId = Guid.NewGuid(),
                Name = "Test Suite",
                Description = "Description",
                CreatedById = Guid.NewGuid(),
                CreatedDate = DateTime.UtcNow,
                TestSuiteScenarios = new List<TestSuiteScenario>
                {
                    new TestSuiteScenario
                    {
                        Id = Guid.NewGuid(),
                        ScenarioId = scenarioId1,
                        DisplayOrder = 2,
                        Scenario = new Scenario { Id = scenarioId1, Title = "Scenario 1", Description = "Desc 1" }
                    },
                    new TestSuiteScenario
                    {
                        Id = Guid.NewGuid(),
                        ScenarioId = scenarioId2,
                        DisplayOrder = 1,
                        Scenario = new Scenario { Id = scenarioId2, Title = "Scenario 2", Description = "Desc 2" }
                    }
                }
            };

            _mockRepository.Setup(r => r.GetWithCasesAsync(testSuiteId))
                .ReturnsAsync(testSuite);

            var query = new GetTestSuiteWithCasesQuery(testSuiteId);

            // Act
            var result = await _handler.Handle(query, CancellationToken.None);

            // Assert
            result.Should().NotBeNull();
            result!.Id.Should().Be(testSuiteId);
            result.Name.Should().Be("Test Suite");
            result.Scenarios.Should().HaveCount(2);
        }

        [Fact]
        public async Task Handle_NonExistentTestSuite_ReturnsNull()
        {
            // Arrange
            var testSuiteId = Guid.NewGuid();
            _mockRepository.Setup(r => r.GetWithCasesAsync(testSuiteId))
                .ReturnsAsync((TestSuite?)null);

            var query = new GetTestSuiteWithCasesQuery(testSuiteId);

            // Act
            var result = await _handler.Handle(query, CancellationToken.None);

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public async Task Handle_ScenariosOrderedByDisplayOrder()
        {
            // Arrange
            var testSuiteId = Guid.NewGuid();
            var testSuite = new TestSuite
            {
                Id = testSuiteId,
                ProjectId = Guid.NewGuid(),
                Name = "Test Suite",
                Description = "Description",
                CreatedById = Guid.NewGuid(),
                TestSuiteScenarios = new List<TestSuiteScenario>
                {
                    new TestSuiteScenario
                    {
                        Id = Guid.NewGuid(),
                        ScenarioId = Guid.NewGuid(),
                        DisplayOrder = 3,
                        Scenario = new Scenario { Title = "Third", Description = "" }
                    },
                    new TestSuiteScenario
                    {
                        Id = Guid.NewGuid(),
                        ScenarioId = Guid.NewGuid(),
                        DisplayOrder = 1,
                        Scenario = new Scenario { Title = "First", Description = "" }
                    },
                    new TestSuiteScenario
                    {
                        Id = Guid.NewGuid(),
                        ScenarioId = Guid.NewGuid(),
                        DisplayOrder = 2,
                        Scenario = new Scenario { Title = "Second", Description = "" }
                    }
                }
            };

            _mockRepository.Setup(r => r.GetWithCasesAsync(testSuiteId))
                .ReturnsAsync(testSuite);

            var query = new GetTestSuiteWithCasesQuery(testSuiteId);

            // Act
            var result = await _handler.Handle(query, CancellationToken.None);

            // Assert
            result.Should().NotBeNull();
            result!.Scenarios.Should().HaveCount(3);
            result.Scenarios[0].ScenarioTitle.Should().Be("First");
            result.Scenarios[1].ScenarioTitle.Should().Be("Second");
            result.Scenarios[2].ScenarioTitle.Should().Be("Third");
        }

        [Fact]
        public async Task Handle_TestSuiteWithNoScenarios_ReturnsEmptyList()
        {
            // Arrange
            var testSuiteId = Guid.NewGuid();
            var testSuite = new TestSuite
            {
                Id = testSuiteId,
                ProjectId = Guid.NewGuid(),
                Name = "Empty Suite",
                Description = "No scenarios",
                CreatedById = Guid.NewGuid(),
                TestSuiteScenarios = new List<TestSuiteScenario>()
            };

            _mockRepository.Setup(r => r.GetWithCasesAsync(testSuiteId))
                .ReturnsAsync(testSuite);

            var query = new GetTestSuiteWithCasesQuery(testSuiteId);

            // Act
            var result = await _handler.Handle(query, CancellationToken.None);

            // Assert
            result.Should().NotBeNull();
            result!.Scenarios.Should().BeEmpty();
        }
    }
}
