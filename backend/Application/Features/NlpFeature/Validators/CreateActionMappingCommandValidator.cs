// Validator for CreateActionMappingCommand.
// Ensures all required fields are provided and the action type is valid.

using Application.Common.Validator;
using Application.Features.NlpFeature.Commands;
using Domain.Enums;
using FluentValidation;

namespace Application.Features.NlpFeature.Validators
{
    /// <summary>
    /// Validates CreateActionMappingCommand fields using FluentValidation rules.
    /// Each RuleFor defines a constraint on a specific field.
    /// </summary>
    public class CreateActionMappingCommandValidator : ValidatorBase<CreateActionMappingCommand>
    {
        public CreateActionMappingCommandValidator()
        {
            // ProjectId must not be an empty GUID (Guid.Empty = "00000000-0000-0000-0000-000000000000")
            RuleFor(v => v.ProjectId)
                .NotEmpty()
                .WithMessage("Project ID is required.");

            // IntentPattern is the regex that matches step intents — must be provided
            RuleFor(v => v.IntentPattern)
                .NotEmpty()
                .WithMessage("Intent pattern is required.");

            // ActionType must be a valid UIActionType enum value
            RuleFor(v => v.ActionType)
                .NotEmpty()
                .WithMessage("Action type is required.")
                .Must(actionType => Enum.TryParse<UIActionType>(actionType, ignoreCase: true, out _))
                .WithMessage($"Invalid action type. Valid values: {string.Join(", ", Enum.GetNames<UIActionType>())}");

            // SelectorStrategy tells the automation engine how to find the element
            RuleFor(v => v.SelectorStrategy)
                .NotEmpty()
                .WithMessage("Selector strategy is required (e.g., 'css', 'xpath', 'id').");

            // SelectorValue is the actual selector (e.g., "#loginBtn")
            RuleFor(v => v.SelectorValue)
                .NotEmpty()
                .WithMessage("Selector value is required.");

            // Description helps developers understand what this mapping does
            RuleFor(v => v.Description)
                .NotEmpty()
                .WithMessage("Description is required.");
        }
    }
}
