using Application.Common.Constants;
using Application.Common.Validator;
using Application.Features.UserFeature.Commands;
using FluentValidation;
namespace Application.Features.UserFeature.Validators
{
    public class AddUserRegisterValidator : ValidatorBase<AddUserRegister>
    {
        public AddUserRegisterValidator()
        {
            RuleFor(v => v.FirstName)
                 .NotEmpty().WithMessage(ValidationConstants.FirstNameMustHasValue)
                 .MinimumLength(3).WithMessage("First name must be at least 3 characters.")
                 .MaximumLength(100).WithMessage("First name must not exceed 100 characters.");

            RuleFor(v => v.LastName).NotEmpty()
                .WithMessage(ValidationConstants.LastNameMustHasValue)
                .MinimumLength(3).WithMessage("Last name must be at least 3 characters.")
                .MaximumLength(100).WithMessage("Last name must not exceed 100 characters.");

            RuleFor(v => v.Email).NotEmpty()
                .WithMessage(ValidationConstants.EmailMustHasValue)
                .EmailAddress().WithMessage("Invalid email format.")
                .MaximumLength(150).WithMessage("Email must not exceed 150 characters.");

            RuleFor(v => v.Password).NotEmpty()
                .WithMessage("Password must have a value.")
                .MinimumLength(8).WithMessage("Password must be at least 8 characters long.")
                .Matches(@"[A-Z]").WithMessage("Password must contain at least one uppercase letter.")
                .Matches(@"[a-z]").WithMessage("Password must contain at least one lowercase letter.")
                .Matches(@"[0-9]").WithMessage("Password must contain at least one number.")
                .Matches(@"[\W_]").WithMessage("Password must contain at least one special character.");
        }
    
    }
}
