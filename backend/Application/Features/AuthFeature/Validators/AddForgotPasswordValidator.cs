using Application.Features.AuthFeature.Commands;
using FluentValidation;

namespace Application.Features.AuthFeature.Validators
{
    public class AddForgotPasswordValidator : AbstractValidator<AddForgotPassword>
    {
        public AddForgotPasswordValidator()
        {
            RuleFor(x => x.Email)
                .NotEmpty().WithMessage("Email is required.")
                .EmailAddress().WithMessage("Invalid email format.")
                .MaximumLength(150).WithMessage("Email must not exceed 150 characters.");
        }
    }
}