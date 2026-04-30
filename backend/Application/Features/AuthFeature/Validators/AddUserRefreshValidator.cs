using FluentValidation;

namespace Application.Features.UserFeature.Commands
{
    public class AddUserRefreshValidator : AbstractValidator<AddUserRefresh>
    {
        public AddUserRefreshValidator()
        {
            RuleFor(x => x.RefreshToken)
                .NotEmpty().WithMessage("Le refresh token est requis.")
                .NotNull().WithMessage("Le refresh token ne peut pas être null.")
                .MinimumLength(20).WithMessage("Le refresh token semble invalide.");
        }
    }
}
