namespace Domain.Interfaces.Services
{
    /// <summary>
    /// Interface pour le parser Gherkin.
    /// </summary>
    public interface IGherkinParserService
    {
        (string FeatureName, IReadOnlyList<ParsedScenario> Scenarios) ParseFeatureContent(string gherkinContent);
        bool ValidateSyntax(string gherkinContent, out IReadOnlyList<string> errors);
    }

    public record ParsedScenario(
        string Name,
        string Description,
        IReadOnlyList<ParsedStep> Steps,
        IReadOnlyList<string> Tags
    );

    public record ParsedStep(
        string Keyword, // Given, When, Then, And, But
        string Text,
        string? DataTable
    );

}
