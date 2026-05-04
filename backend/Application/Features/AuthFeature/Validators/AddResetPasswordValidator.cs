using Application.Features.AuthFeature.Commands;
using FluentValidation;

namespace Application.Features.AuthFeature.Validators
{
    public class AddResetPasswordValidator : AbstractValidator<AddResetPassword>
    {
        public AddResetPasswordValidator()
        {
            RuleFor(x => x.Token)
                .NotEmpty().WithMessage("Token is required.");

            RuleFor(x => x.NewPassword)
                .NotEmpty().WithMessage("Password is required.")
                .MinimumLength(8).WithMessage("Password must be at least 8 characters.")
                .Matches(@"[A-Z]").WithMessage("Password must contain at least one uppercase letter.")
                .Matches(@"[a-z]").WithMessage("Password must contain at least one lowercase letter.")
                .Matches(@"[0-9]").WithMessage("Password must contain at least one number.")
                .Matches(@"[\W_]").WithMessage("Password must contain at least one special character.");

            RuleFor(x => x.ConfirmPassword)
                .NotEmpty().WithMessage("Please confirm your password.");
        }
    }
}