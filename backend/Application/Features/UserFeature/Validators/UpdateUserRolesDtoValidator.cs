using Application.Features.UserFeature.DTOs;
using Application.Users.DTOs;
using FluentValidation;

namespace Application.Features.UserFeature.Validators
{


    public class UpdateUserRolesDtoValidator : AbstractValidator<UpdateUserRolesDto>
    {
        public UpdateUserRolesDtoValidator()
        {
            

            RuleFor(x => x.Roles)
                .NotNull().WithMessage("Les rôles sont obligatoires")
                .Must(r => r.Count > 0).WithMessage("Au moins un rôle doit être défini");
        }
    }
}
