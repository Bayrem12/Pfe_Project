using Application.Common.Validator;
using Application.Features.TestSuitesFeature.Commands;
using FluentValidation;

namespace Application.Features.TestSuitesFeature.Validators
{
    public class AddTestSuiteCommandValidator : ValidatorBase<AddTestSuiteCommand>
    {
        public AddTestSuiteCommandValidator()
        {
            RuleFor(x => x.ProjectId)
                .NotEmpty()
                .WithMessage("Project ID is required");

            RuleFor(x => x.Name)
                .NotEmpty()
                .WithMessage("Name is required")
                .MaximumLength(200)
                .WithMessage("Name cannot exceed 200 characters");

            RuleFor(x => x.Description)
                .MaximumLength(1000)
                .WithMessage("Description cannot exceed 1000 characters");

            RuleFor(x => x.CreatedById)
                .NotEmpty()
                .WithMessage("CreatedById is required");
        }
    }
}
