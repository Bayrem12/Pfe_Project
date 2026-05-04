// DTO for ActionMapping — this is what the API returns when creating an action mapping.
// Matches the Swagger 201 response: id, intentPattern, actionType, selectorStrategy, selectorValue, description, isActive

namespace Application.Features.NlpFeature.Dtos
{
    /// <summary>
    /// Represents an action mapping that links an NLP intent pattern to a specific UI action.
    /// Example: intentPattern="click.*login" → actionType="Click", selectorStrategy="css", selectorValue="#loginBtn"
    /// This tells the automation engine: "When the NLP detects a click-login intent, click the #loginBtn element"
    /// </summary>
    public class ActionMappingDto
    {
        // Unique ID of this action mapping
        public Guid Id { get; set; }

        // Regex or pattern that matches step intents — e.g., "click.*login"
        public string IntentPattern { get; set; } = string.Empty;

        // The type of UI action to perform — "Click", "Type", "Navigate", etc.
        public string ActionType { get; set; } = string.Empty;

        // How to find the element — "css", "xpath", "id", etc.
        public string SelectorStrategy { get; set; } = string.Empty;

        // The actual selector value — e.g., "#loginBtn", "//button[@id='login']"
        public string SelectorValue { get; set; } = string.Empty;

        // Human-readable description of what this mapping does
        public string Description { get; set; } = string.Empty;

        // Whether this mapping is currently active (can be disabled without deleting)
        public bool IsActive { get; set; }

        // Higher value = evaluated first when multiple active mappings match the same step
        public int Priority { get; set; }
    }
}
