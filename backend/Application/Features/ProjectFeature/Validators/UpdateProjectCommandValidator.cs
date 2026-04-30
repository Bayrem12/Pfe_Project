using Application.Common.Constants;
using Application.Common.Validator;
using Application.Features.ProjectFeature.Commands;
using FluentValidation;

namespace Application.Features.ProjectFeature.Validators
{
    public class UpdateProjectCommandValidator : ValidatorBase<UpdateProjectCommand>
    {
        public UpdateProjectCommandValidator()
        {
            RuleFor(v => v.ProjectId)
                .NotEmpty()
                .WithMessage(ValidationConstants.ProjectIdMustHasValue);

            RuleFor(v => v.Name)
                .NotEmpty()
                .WithMessage(ValidationConstants.ProjectNameMustHasValue)
                .MaximumLength(200)
                .WithMessage(ValidationConstants.ProjectNameMaxLength);

            RuleFor(v => v.Description)
                .MaximumLength(1000)
                .WithMessage(ValidationConstants.DescriptionMaxLength);
        }
    }
}
