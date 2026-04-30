using Application.Common.Validator;
using Application.Features.ScenariosFeature.Commands;
using FluentValidation;

namespace Application.Features.ScenariosFeature.Validators
{
    public class CreateScenarioCommandValidator : ValidatorBase<CreateScenarioCommand>
    {
        public CreateScenarioCommandValidator()
        {
            RuleFor(x => x.FeatureId)
                .NotEmpty()
                .WithMessage("Feature ID is required");

            RuleFor(x => x.Title)
                .NotEmpty()
                .WithMessage("Title is required")
                .MaximumLength(200)
                .WithMessage("Title cannot exceed 200 characters");

            RuleFor(x => x.Description)
                .MaximumLength(1000)
                .WithMessage("Description cannot exceed 1000 characters");

            RuleFor(x => x.GherkinContent)
                .NotEmpty()
                .WithMessage("Gherkin content is required");

        }
    }
}