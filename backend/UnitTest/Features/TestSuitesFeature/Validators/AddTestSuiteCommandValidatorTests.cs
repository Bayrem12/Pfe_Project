using Application.Features.TestSuitesFeature.Commands;
using Application.Features.TestSuitesFeature.Validators;
using FluentAssertions;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace UnitTest.Features.TestSuitesFeature.Validators
{
    public class AddTestSuiteCommandValidatorTests
    {
        private readonly AddTestSuiteCommandValidator _validator;

        public AddTestSuiteCommandValidatorTests()
        {
            _validator = new AddTestSuiteCommandValidator();
        }

        [Fact]
        public void Validate_ValidCommand_ReturnsStatus200()
        {
            // Arrange
            var command = new AddTestSuiteCommand(
                Guid.NewGuid(),
                "Valid Name",
                "Valid Description",
                Guid.NewGuid());

            // Act
            var result = _validator.Validate(new ValidationContext<AddTestSuiteCommand>(command));

            // Assert
            result.Status.Should().Be(StatusCodes.Status200OK);
        }

        [Fact]
        public void Validate_EmptyProjectId_ReturnsStatus400()
        {
            // Arrange
            var command = new AddTestSuiteCommand(
                Guid.Empty,
                "Valid Name",
                "Valid Description",
                Guid.NewGuid());

            // Act
            var result = _validator.Validate(new ValidationContext<AddTestSuiteCommand>(command));

            // Assert
            result.Status.Should().Be(StatusCodes.Status400BadRequest);
            result.FailMessages.Should().Contain("Project ID is required");
        }

        [Fact]
        public void Validate_EmptyName_ReturnsStatus400()
        {
            // Arrange
            var command = new AddTestSuiteCommand(
                Guid.NewGuid(),
                "",
                "Valid Description",
                Guid.NewGuid());

            // Act
            var result = _validator.Validate(new ValidationContext<AddTestSuiteCommand>(command));

            // Assert
            result.Status.Should().Be(StatusCodes.Status400BadRequest);
            result.FailMessages.Should().Contain("Name is required");
        }

        [Fact]
        public void Validate_NameExceeds200Characters_ReturnsStatus400()
        {
            // Arrange
            var longName = new string('x', 201);
            var command = new AddTestSuiteCommand(
                Guid.NewGuid(),
                longName,
                "Valid Description",
                Guid.NewGuid());

            // Act
            var result = _validator.Validate(new ValidationContext<AddTestSuiteCommand>(command));

            // Assert
            result.Status.Should().Be(StatusCodes.Status400BadRequest);
            result.FailMessages.Should().Contain("Name cannot exceed 200 characters");
        }

        [Fact]
        public void Validate_DescriptionExceeds1000Characters_ReturnsStatus400()
        {
            // Arrange
            var longDescription = new string('x', 1001);
            var command = new AddTestSuiteCommand(
                Guid.NewGuid(),
                "Valid Name",
                longDescription,
                Guid.NewGuid());

            // Act
            var result = _validator.Validate(new ValidationContext<AddTestSuiteCommand>(command));

            // Assert
            result.Status.Should().Be(StatusCodes.Status400BadRequest);
            result.FailMessages.Should().Contain("Description cannot exceed 1000 characters");
        }

        [Fact]
        public void Validate_EmptyCreatedById_ReturnsStatus400()
        {
            // Arrange
            var command = new AddTestSuiteCommand(
                Guid.NewGuid(),
                "Valid Name",
                "Valid Description",
                Guid.Empty);

            // Act
            var result = _validator.Validate(new ValidationContext<AddTestSuiteCommand>(command));

            // Assert
            result.Status.Should().Be(StatusCodes.Status400BadRequest);
            result.FailMessages.Should().Contain("CreatedById is required");
        }

        [Fact]
        public void Validate_Name200Characters_ReturnsStatus200()
        {
            // Arrange
            var name200Chars = new string('x', 200);
            var command = new AddTestSuiteCommand(
                Guid.NewGuid(),
                name200Chars,
                "Valid Description",
                Guid.NewGuid());

            // Act
            var result = _validator.Validate(new ValidationContext<AddTestSuiteCommand>(command));

            // Assert
            result.Status.Should().Be(StatusCodes.Status200OK);
        }

        [Fact]
        public void Validate_Description1000Characters_ReturnsStatus200()
        {
            // Arrange
            var description1000Chars = new string('x', 1000);
            var command = new AddTestSuiteCommand(
                Guid.NewGuid(),
                "Valid Name",
                description1000Chars,
                Guid.NewGuid());

            // Act
            var result = _validator.Validate(new ValidationContext<AddTestSuiteCommand>(command));

            // Assert
            result.Status.Should().Be(StatusCodes.Status200OK);
        }
    }
}
