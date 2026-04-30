using Application.Common.Constants;
using Application.Common.Validator;
using Application.Features.FeatureFeature.Commands;
using FluentValidation;

namespace Application.Features.FeatureFeature.Validators
{
    public class AddFeatureCommandValidator : ValidatorBase<AddFeatureCommand>
    {
        public AddFeatureCommandValidator()
        {
            RuleFor(v => v.ModuleId)
                .NotEmpty()
                .WithMessage("Module ID is required");

            RuleFor(v => v.Name)
                .NotEmpty()
                .WithMessage("Feature name is required")
                .MaximumLength(200)
                .WithMessage("Feature name cannot exceed 200 characters");

            RuleFor(v => v.Description)
                .MaximumLength(1000)
                .WithMessage("Description cannot exceed 1000 characters");

            RuleFor(v => v.DisplayOrder)
                .GreaterThanOrEqualTo(0)
                .WithMessage("Display order must be a positive number");

            //RuleFor(v => v.CreatedById)
                //.NotEmpty()
                //.WithMessage("Creator user ID is required");
        }
    }

    public class UpdateFeatureCommandValidator : ValidatorBase<UpdateFeatureCommand>
    {
        public UpdateFeatureCommandValidator()
        {
            RuleFor(v => v.FeatureId)
                .NotEmpty()
                .WithMessage("Feature ID is required");

            RuleFor(v => v.Name)
                .NotEmpty()
                .WithMessage("Feature name is required")
                .MaximumLength(200)
                .WithMessage("Feature name cannot exceed 200 characters");

            RuleFor(v => v.Description)
                .MaximumLength(1000)
                .WithMessage("Description cannot exceed 1000 characters");

            RuleFor(v => v.DisplayOrder)
                .GreaterThanOrEqualTo(0)
                .WithMessage("Display order must be a positive number");

            RuleFor(v => v.UpdatedById)
                .NotEmpty()
                .WithMessage("Updater user ID is required");
        }
    }
}
