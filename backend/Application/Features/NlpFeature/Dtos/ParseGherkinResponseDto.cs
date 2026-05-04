// DTO for the Gherkin parse response.
// POST /api/Nlp/parse expects { gherkinContent: "string" } and returns parsed steps.

namespace Application.Features.NlpFeature.Dtos
{
    /// <summary>
    /// Represents a single step parsed from Gherkin text.
    /// Example Gherkin: "Given I navigate to the login page" → keyword="Given", text="I navigate to the login page"
    /// </summary>
    public class ParsedStepDto
    {
        // The Gherkin keyword — "Given", "When", "Then", "And", "But"
        public string Keyword { get; set; } = string.Empty;

        // The step text after the keyword — "I navigate to the login page"
        public string Text { get; set; } = string.Empty;
    }

    /// <summary>
    /// Response returned after parsing Gherkin content.
    /// Contains the list of all parsed steps from the Gherkin text.
    /// </summary>
    public class ParseGherkinResponseDto
    {
        // Whether the parsing was successful
        public bool IsValid { get; set; }

        // The list of parsed steps
        public List<ParsedStepDto> Steps { get; set; } = new();

        // Error message if parsing failed
        public string? ErrorMessage { get; set; }
    }
}
