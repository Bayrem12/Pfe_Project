using Application.Features.UserFeature.Commands;
using FluentValidation;

namespace Application.Features.UserFeature.Validators
{
    public class AddUserLoginValidator : AbstractValidator<AddUserLogin>
    {
        public AddUserLoginValidator()
        {
            RuleFor(x => x.Email)
                .NotEmpty().WithMessage("L'email est requis.")
                .NotNull().WithMessage("L'email ne peut pas être null.")
                .EmailAddress().WithMessage("Format d'email invalide.");

            RuleFor(x => x.Password)
                .NotEmpty().WithMessage("Le mot de passe est requis.")
                .NotNull().WithMessage("Le mot de passe ne peut pas être null.")
                .MinimumLength(6).WithMessage("Le mot de passe doit contenir au moins 6 caractères.");
        }
    }
}