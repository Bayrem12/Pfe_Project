using System.Text.RegularExpressions;
using Domain.Interfaces.Services;


namespace Persistance.Services;

/// <summary>
/// Service de parsing de contenu Gherkin. 
/// Parse les fichiers .feature en structures exploitables.
/// </summary>
public class GherkinParserService : IGherkinParserService
{
    private static readonly Regex FeatureRegex = new(@"^\s*Feature:\s*(.+)$", RegexOptions.Multiline);
    private static readonly Regex ScenarioRegex = new(@"^\s*(Scenario|Scenario Outline):\s*(.+)$", RegexOptions.Multiline);
    private static readonly Regex StepRegex = new(@"^\s*(Given|When|Then|And|But)\s+(.+)$", RegexOptions.Multiline);
    private static readonly Regex TagRegex = new(@"^\s*@(\S+)", RegexOptions.Multiline);
    private static readonly Regex DataTableRowRegex = new(@"^\s*\|.+\|$", RegexOptions.Multiline);

    public (string FeatureName, IReadOnlyList<ParsedScenario> Scenarios) ParseFeatureContent(string gherkinContent)
    {
        if (string.IsNullOrWhiteSpace(gherkinContent))
            throw new ArgumentException("Le contenu Gherkin ne peut pas être vide.");

        var featureMatch = FeatureRegex.Match(gherkinContent);
        var featureName = featureMatch.Success ? featureMatch.Groups[1].Value.Trim() : "Sans nom";

        var scenarios = new List<ParsedScenario>();
        var scenarioMatches = ScenarioRegex.Matches(gherkinContent);

        for (int i = 0; i < scenarioMatches.Count; i++)
        {
            var match = scenarioMatches[i];
            var scenarioName = match.Groups[2].Value.Trim();

            // Determine the text block for this scenario
            var startIdx = match.Index + match.Length;
            var endIdx = (i + 1 < scenarioMatches.Count) ? scenarioMatches[i + 1].Index : gherkinContent.Length;
            var scenarioBlock = gherkinContent[startIdx..endIdx];

            // Extract tags before the scenario line
            var tagBlock = gherkinContent[..match.Index];
            if (i > 0)
                tagBlock = gherkinContent[scenarioMatches[i - 1].Index..match.Index];

            var tags = TagRegex.Matches(tagBlock).Select(m => m.Groups[1].Value).ToList();

            // Extract steps with DataTable support
            var stepMatches = StepRegex.Matches(scenarioBlock);
            var steps = new List<ParsedStep>();

            for (int j = 0; j < stepMatches.Count; j++)
            {
                var sm = stepMatches[j];
                var keyword = sm.Groups[1].Value;
                var text = sm.Groups[2].Value.Trim();

                // Check for DataTable lines after this step (before next step or end of block)
                var stepEnd = sm.Index + sm.Length;
                var nextStepStart = (j + 1 < stepMatches.Count) ? stepMatches[j + 1].Index : scenarioBlock.Length;
                var betweenSteps = scenarioBlock[stepEnd..nextStepStart];

                string? dataTable = null;
                var tableRows = DataTableRowRegex.Matches(betweenSteps);
                if (tableRows.Count > 0)
                {
                    dataTable = string.Join("\n", tableRows.Select(r => r.Value.Trim()));
                }

                steps.Add(new ParsedStep(keyword, text, dataTable));
            }

            scenarios.Add(new ParsedScenario(scenarioName, string.Empty, steps, tags));
        }

        return (featureName, scenarios);
    }

    public bool ValidateSyntax(string gherkinContent, out IReadOnlyList<string> errors)
    {
        var errs = new List<string>();

        if (string.IsNullOrWhiteSpace(gherkinContent))
        {
            errs.Add("Le contenu Gherkin est vide.");
            errors = errs;
            return false;
        }

        // Check for Feature keyword
        if (!FeatureRegex.IsMatch(gherkinContent))
            errs.Add("Mot-clé 'Feature:' manquant.");

        // Check for at least one scenario
        if (!ScenarioRegex.IsMatch(gherkinContent))
            errs.Add("Aucun scénario trouvé. Ajoutez au moins un 'Scenario:'.");

        // Check each scenario has at least one step
        var scenarioMatches = ScenarioRegex.Matches(gherkinContent);
        for (int i = 0; i < scenarioMatches.Count; i++)
        {
            var startIdx = scenarioMatches[i].Index + scenarioMatches[i].Length;
            var endIdx = (i + 1 < scenarioMatches.Count) ? scenarioMatches[i + 1].Index : gherkinContent.Length;
            var block = gherkinContent[startIdx..endIdx];

            if (!StepRegex.IsMatch(block))
                errs.Add($"Le scénario '{scenarioMatches[i].Groups[2].Value.Trim()}' ne contient aucun step.");
        }

        errors = errs;
        return errs.Count == 0;
    }
}
