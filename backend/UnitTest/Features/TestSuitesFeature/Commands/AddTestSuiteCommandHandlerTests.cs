using Application.Features.TestSuitesFeature.Commands;
using Application.Interfaces;
using Domain.Entities.ProjectManagement;
using Domain.Entities.Scenarios;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Moq;
using Xunit;

namespace UnitTest.Features.TestSuitesFeature.Commands
{
    public class AddTestSuiteCommandHandlerTests
    {
        private readonly Mock<ITestSuiteRepository> _mockRepository;
        private readonly Mock<ITestTestAutoumatisationContext> _mockContext;
        private readonly AddTestSuiteCommandHandler _handler;

        public AddTestSuiteCommandHandlerTests()
        {
            _mockRepository = new Mock<ITestSuiteRepository>();
            _mockContext = new Mock<ITestTestAutoumatisationContext>();
            _handler = new AddTestSuiteCommandHandler(_mockRepository.Object, _mockContext.Object);
        }

        [Fact]
        public async Task Handle_ValidCommand_ReturnsNewGuid()
        {
            // Arrange
            var projectId = Guid.NewGuid();
            var createdById = Guid.NewGuid();
            var command = new AddTestSuiteCommand(projectId, "Test Suite 1", "Description", createdById);

            var projects = new List<Project> { new Project { Id = projectId, Name = "Test Project" } }.AsQueryable();
            var mockProjectDbSet = CreateMockDbSet(projects);
            _mockContext.Setup(c => c.Projects).Returns(mockProjectDbSet.Object);

            var testSuites = new List<TestSuite>().AsQueryable();
            var mockTestSuiteDbSet = CreateMockDbSet(testSuites);
            _mockContext.Setup(c => c.TestSuites).Returns(mockTestSuiteDbSet.Object);

            _mockRepository.Setup(r => r.AddAsync(It.IsAny<TestSuite>()))
                .ReturnsAsync((TestSuite ts) => ts);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.Should().NotBeEmpty();
            _mockRepository.Verify(r => r.AddAsync(It.Is<TestSuite>(ts =>
                ts.Name == "Test Suite 1" &&
                ts.ProjectId == projectId &&
                ts.CreatedById == createdById)), Times.Once);
        }

        [Fact]
        public async Task Handle_ProjectNotFound_ThrowsInvalidOperationException()
        {
            // Arrange
            var projectId = Guid.NewGuid();
            var command = new AddTestSuiteCommand(projectId, "Test Suite 1", "Description", Guid.NewGuid());

            var projects = new List<Project>().AsQueryable();
            var mockProjectDbSet = CreateMockDbSet(projects);
            _mockContext.Setup(c => c.Projects).Returns(mockProjectDbSet.Object);

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _handler.Handle(command, CancellationToken.None));
        }

        [Fact]
        public async Task Handle_DuplicateName_ThrowsInvalidOperationException()
        {
            // Arrange
            var projectId = Guid.NewGuid();
            var command = new AddTestSuiteCommand(projectId, "Existing Suite", "Description", Guid.NewGuid());

            var projects = new List<Project> { new Project { Id = projectId, Name = "Test Project" } }.AsQueryable();
            var mockProjectDbSet = CreateMockDbSet(projects);
            _mockContext.Setup(c => c.Projects).Returns(mockProjectDbSet.Object);

            var existingTestSuites = new List<TestSuite>
            {
                new TestSuite { Id = Guid.NewGuid(), ProjectId = projectId, Name = "Existing Suite", IsDeleted = false }
            }.AsQueryable();
            var mockTestSuiteDbSet = CreateMockDbSet(existingTestSuites);
            _mockContext.Setup(c => c.TestSuites).Returns(mockTestSuiteDbSet.Object);

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _handler.Handle(command, CancellationToken.None));
        }

        [Fact]
        public async Task Handle_TrimsNameBeforeSaving()
        {
            // Arrange
            var projectId = Guid.NewGuid();
            var command = new AddTestSuiteCommand(projectId, "  Test Suite  ", "Description", Guid.NewGuid());

            var projects = new List<Project> { new Project { Id = projectId, Name = "Test Project" } }.AsQueryable();
            var mockProjectDbSet = CreateMockDbSet(projects);
            _mockContext.Setup(c => c.Projects).Returns(mockProjectDbSet.Object);

            var testSuites = new List<TestSuite>().AsQueryable();
            var mockTestSuiteDbSet = CreateMockDbSet(testSuites);
            _mockContext.Setup(c => c.TestSuites).Returns(mockTestSuiteDbSet.Object);

            _mockRepository.Setup(r => r.AddAsync(It.IsAny<TestSuite>()))
                .ReturnsAsync((TestSuite ts) => ts);

            // Act
            await _handler.Handle(command, CancellationToken.None);

            // Assert
            _mockRepository.Verify(r => r.AddAsync(It.Is<TestSuite>(ts =>
                ts.Name == "Test Suite")), Times.Once);
        }

        private static Mock<DbSet<T>> CreateMockDbSet<T>(IQueryable<T> data) where T : class
        {
            var mockSet = new Mock<DbSet<T>>();
            mockSet.As<IAsyncEnumerable<T>>()
                .Setup(m => m.GetAsyncEnumerator(It.IsAny<CancellationToken>()))
                .Returns(new TestAsyncEnumerator<T>(data.GetEnumerator()));
            mockSet.As<IQueryable<T>>().Setup(m => m.Provider).Returns(new TestAsyncQueryProvider<T>(data.Provider));
            mockSet.As<IQueryable<T>>().Setup(m => m.Expression).Returns(data.Expression);
            mockSet.As<IQueryable<T>>().Setup(m => m.ElementType).Returns(data.ElementType);
            mockSet.As<IQueryable<T>>().Setup(m => m.GetEnumerator()).Returns(data.GetEnumerator());
            return mockSet;
        }
    }

    // Helper classes for async EF Core mocking
    internal class TestAsyncQueryProvider<TEntity> : IAsyncQueryProvider
    {
        private readonly IQueryProvider _inner;

        internal TestAsyncQueryProvider(IQueryProvider inner) => _inner = inner;

        public IQueryable CreateQuery(System.Linq.Expressions.Expression expression)
            => new TestAsyncEnumerable<TEntity>(expression);

        public IQueryable<TElement> CreateQuery<TElement>(System.Linq.Expressions.Expression expression)
            => new TestAsyncEnumerable<TElement>(expression);

        public object? Execute(System.Linq.Expressions.Expression expression)
            => _inner.Execute(expression);

        public TResult Execute<TResult>(System.Linq.Expressions.Expression expression)
            => _inner.Execute<TResult>(expression);

        public TResult ExecuteAsync<TResult>(System.Linq.Expressions.Expression expression, CancellationToken cancellationToken = default)
        {
            var resultType = typeof(TResult).GetGenericArguments()[0];
            var executionResult = typeof(IQueryProvider)
                .GetMethod(name: nameof(IQueryProvider.Execute), genericParameterCount: 1, types: new[] { typeof(System.Linq.Expressions.Expression) })!
                .MakeGenericMethod(resultType)
                .Invoke(this, new[] { expression });

            return (TResult)typeof(Task).GetMethod(nameof(Task.FromResult))!
                .MakeGenericMethod(resultType)
                .Invoke(null, new[] { executionResult })!;
        }
    }

    internal class TestAsyncEnumerable<T> : EnumerableQuery<T>, IAsyncEnumerable<T>, IQueryable<T>
    {
        public TestAsyncEnumerable(IEnumerable<T> enumerable) : base(enumerable) { }
        public TestAsyncEnumerable(System.Linq.Expressions.Expression expression) : base(expression) { }

        public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
            => new TestAsyncEnumerator<T>(this.AsEnumerable().GetEnumerator());

        IQueryProvider IQueryable.Provider => new TestAsyncQueryProvider<T>(this);
    }

    internal class TestAsyncEnumerator<T> : IAsyncEnumerator<T>
    {
        private readonly IEnumerator<T> _inner;

        public TestAsyncEnumerator(IEnumerator<T> inner) => _inner = inner;

        public T Current => _inner.Current;

        public ValueTask<bool> MoveNextAsync() => new ValueTask<bool>(_inner.MoveNext());

        public ValueTask DisposeAsync()
        {
            _inner.Dispose();
            return new ValueTask();
        }
    }
}
