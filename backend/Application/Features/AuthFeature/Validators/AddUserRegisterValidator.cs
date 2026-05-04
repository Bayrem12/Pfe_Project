using Application.Common.Constants;
using Application.Common.Validator;
using Application.Features.UserFeature.Commands;
using FluentValidation;
namespace Application.Features.UserFeature.Validators
{
    public class AddUserRegisterValidator : ValidatorBase<AddUserRegister>
    {
        private static readonly HashSet<string> CommonPasswords = new(StringComparer.OrdinalIgnoreCase)
        {
            "123456", "password", "123456789", "12345678", "12345", "1234567",
            "password1", "abc123", "qwerty", "azerty", "letmein", "iloveyou",
            "admin", "welcome", "monkey", "login", "sunshine", "master"
        };

        public AddUserRegisterValidator()
        {
            RuleFor(v => v.FirstName)
                .NotEmpty().WithMessage("Le prénom est obligatoire.")
                .MinimumLength(3).WithMessage("Le prénom doit contenir au moins 3 caractères.")
                .MaximumLength(20).WithMessage("Le prénom ne doit pas dépasser 20 caractères.")
                .Matches(@"^[a-zA-ZÀ-ÿ][a-zA-ZÀ-ÿ0-9]*$")
                    .WithMessage("Le prénom doit commencer par une lettre et ne contenir que des lettres et chiffres (sans espaces).");

            RuleFor(v => v.LastName)
                .NotEmpty().WithMessage("Le nom est obligatoire.")
                .MinimumLength(3).WithMessage("Le nom doit contenir au moins 3 caractères.")
                .MaximumLength(20).WithMessage("Le nom ne doit pas dépasser 20 caractères.")
                .Matches(@"^[a-zA-ZÀ-ÿ][a-zA-ZÀ-ÿ0-9]*$")
                    .WithMessage("Le nom doit commencer par une lettre et ne contenir que des lettres et chiffres (sans espaces).");

            RuleFor(v => v.Email)
                .NotEmpty().WithMessage("L'adresse e-mail est obligatoire.")
                .EmailAddress().WithMessage("Format d'email invalide.")
                .MaximumLength(150).WithMessage("L'email ne doit pas dépasser 150 caractères.");

            RuleFor(v => v.Password)
                .NotEmpty().WithMessage("Le mot de passe est obligatoire.")
                .MinimumLength(6).WithMessage("Le mot de passe doit contenir au moins 6 caractères.")
                .Matches(@"[A-Za-z]").WithMessage("Le mot de passe doit contenir au moins une lettre.")
                .Matches(@"[0-9]").WithMessage("Le mot de passe doit contenir au moins un chiffre.")
                .Must(p => !CommonPasswords.Contains(p))
                    .WithMessage("Ce mot de passe est trop simple. Choisissez un mot de passe plus sécurisé.");
        }

    }
}
