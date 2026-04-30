using Application.Features.UserFeature.Commands;
using FluentValidation;

namespace Application.Features.UserFeature.Validators
{
    public class AddUserChangePasswordValidator
        : AbstractValidator<AddUserChangePassword>
    {
        public AddUserChangePasswordValidator()
        {
            // Email
            RuleFor(x => x.Email)
                .NotEmpty().WithMessage("L'email est requis.")
                .NotNull().WithMessage("L'email ne peut pas être null.")
                .EmailAddress().WithMessage("Format d'email invalide.");

            // Current Password
            RuleFor(x => x.CurrentPassword)
                .NotEmpty().WithMessage("Le mot de passe courant est requis.")
                .NotNull().WithMessage("Le mot de passe courant ne peut pas être null.")
                .MinimumLength(6).WithMessage("Le mot de passe courant est invalide.");

            // New Password
            RuleFor(x => x.NewPassword)
                .NotEmpty().WithMessage("Le nouveau mot de passe est requis.")
                .NotNull().WithMessage("Le nouveau mot de passe ne peut pas être null.")
                .MinimumLength(6).WithMessage("Le nouveau mot de passe doit contenir au moins 6 caractères.")
                .NotEqual(x => x.CurrentPassword)
                .WithMessage("Le nouveau mot de passe doit être différent de l'ancien.");
        }
    }
}