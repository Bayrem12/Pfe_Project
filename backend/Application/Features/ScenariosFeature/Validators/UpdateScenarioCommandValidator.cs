using Application.Common.Validator;
using Application.Features.ScenariosFeature.Commands;
using FluentValidation;

namespace Application.Features.ScenariosFeature.Validators
{
    public class UpdateScenarioCommandValidator : ValidatorBase<UpdateScenarioCommand>
    {
        public UpdateScenarioCommandValidator()
        {
            RuleFor(x => x.Id)
                .NotEmpty()
                .WithMessage("Scenario ID is required");

            RuleFor(x => x.Title)
                .NotEmpty()
                .WithMessage("Title is required")
                .MaximumLength(200)
                .WithMessage("Title cannot exceed 200 characters");

            RuleFor(x => x.GherkinContent)
                .NotEmpty()
                .WithMessage("Gherkin content is required");

        }
    }
}