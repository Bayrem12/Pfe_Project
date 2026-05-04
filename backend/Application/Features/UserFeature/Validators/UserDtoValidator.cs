using FluentValidation;
using Application.Users.DTOs;

    namespace Application.Features.UserFeature.Validators
{

    public class UserDtoValidator : AbstractValidator<UserDto>
    {
        public UserDtoValidator()
        {
            RuleFor(x => x.FirstName)
                .NotEmpty().WithMessage("Le prénom est obligatoire")
                .MaximumLength(50).WithMessage("Le prénom est trop long");

            RuleFor(x => x.LastName)
                .NotEmpty().WithMessage("Le nom est obligatoire")
                .MaximumLength(50).WithMessage("Le nom est trop long");

            RuleFor(x => x.Email)
                .NotEmpty().WithMessage("L'email est obligatoire")
                .EmailAddress().WithMessage("L'email est invalide");
        }
    }
}
