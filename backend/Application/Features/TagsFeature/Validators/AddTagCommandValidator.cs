using Application.Common.Validator;
using Application.Features.TagsFeature.Commands;
using FluentValidation;

namespace Application.Features.TagsFeature.Validators
{
    public class AddTagCommandValidator : ValidatorBase<AddTagCommand>
    {
        public AddTagCommandValidator()
        {
            RuleFor(v => v.Name)
                .NotEmpty()
                .WithMessage("Tag name is required.");
        }
    }
}
