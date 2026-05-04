// DTO for StepAnalysis — this is what the API returns when analyzing a scenario's steps.
// Matches the Swagger contract: id, stepId, intent, action, target, value, confidence, parameters[]

namespace Application.Features.NlpFeature.Dtos
{
    /// <summary>
    /// Represents the NLP analysis result for a single test step.
    /// Contains the detected intent (Click, Navigate, Input...), the action, target element,
    /// confidence score, and any extracted parameters.
    /// </summary>
    public class StepAnalysisDto
    {
        // The unique ID of this analysis record
        public Guid Id { get; set; }

        // The ID of the Step that was analyzed
        public Guid StepId { get; set; }

        // The detected intent — e.g., "Navigate", "Click", "Input" (comes from StepIntentType enum)
        public string Intent { get; set; } = string.Empty;

        // The action to perform — e.g., "click", "type", "navigate"
        public string Action { get; set; } = string.Empty;

        // The target UI element — e.g., "#loginButton", ".username-field"
        public string Target { get; set; } = string.Empty;

        // The value to use (optional) — e.g., "admin@test.com" for an input field
        public string? Value { get; set; }

        // How confident the NLP engine is in this analysis (0.0 to 1.0)
        public double Confidence { get; set; }

        // The list of parameters extracted from the step text
        public List<StepParameterDto> Parameters { get; set; } = new();
    }
}
