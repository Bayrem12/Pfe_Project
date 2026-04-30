using Application.Common.Constants;
using Application.Common.Validator;
using Application.Features.ProjectFeature.Commands;
using Domain.Enums;
using FluentValidation;

namespace Application.Features.ProjectFeature.Validators
{
    public class AddProjectMemberCommandValidator : ValidatorBase<AddProjectMemberCommand>
    {
        public AddProjectMemberCommandValidator()
        {
            RuleFor(v => v.ProjectId)
                .NotEmpty()
                .WithMessage(ValidationConstants.ProjectIdMustHasValue);

            RuleFor(v => v.UserId)
                .NotEmpty()
                .WithMessage(ValidationConstants.UserIdMustHasValue);

            RuleFor(v => v.Role)
                .NotEmpty()
                .Must(role => Enum.TryParse<ProjectRole>(role, true, out _))
                .WithMessage(ValidationConstants.RoleMustHasValue);
        }
    }
}
