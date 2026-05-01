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
                .MinimumLength(4)
                .WithMessage("Le nom du projet doit contenir au moins 4 caractères.")
                .MaximumLength(200)
                .WithMessage(ValidationConstants.ProjectNameMaxLength);

            RuleFor(v => v.Description)
                .MaximumLength(1000)
                .WithMessage(ValidationConstants.DescriptionMaxLength);

            RuleFor(v => v.Url)
                .Must(url => string.IsNullOrWhiteSpace(url) ||
                             url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                             url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                .WithMessage("L'URL doit commencer par http:// ou https://.")
                .MaximumLength(500)
                .When(v => !string.IsNullOrWhiteSpace(v.Url))
                .WithMessage("L'URL ne doit pas dépasser 500 caractères.");
        }
    }
}
