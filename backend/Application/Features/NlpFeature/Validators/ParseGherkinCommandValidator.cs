// Validator for the ParseGherkinCommand.
// FluentValidation ensures the request data is valid BEFORE the Handler runs.
// This prevents bad data from reaching the business logic.

using Application.Common.Validator;
using Application.Features.NlpFeature.Commands;
using FluentValidation;

namespace Application.Features.NlpFeature.Validators
{
    /// <summary>
    /// Validates the ParseGherkinCommand before processing.
    /// Extends ValidatorBase which handles converting validation errors to ResponseHttp.
    /// </summary>
    public class ParseGherkinCommandValidator : ValidatorBase<ParseGherkinCommand>
    {
        public ParseGherkinCommandValidator()
        {
            // The gherkin content must not be empty
            RuleFor(v => v.GherkinContent)
                .NotEmpty()
                .WithMessage("Gherkin content is required and cannot be empty.")
                .MaximumLength(10000)
                .WithMessage("Gherkin content cannot exceed 10000 characters.");
        }
    }
}
