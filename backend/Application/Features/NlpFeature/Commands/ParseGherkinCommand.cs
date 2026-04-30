// COMMAND: Parse Gherkin content into structured steps
// Endpoint: POST /api/Nlp/parse
// Request body: { "gherkinContent": "string" }
//
// This parses raw Gherkin text (Given/When/Then syntax) into individual steps.
// Gherkin is the language used to write BDD (Behavior Driven Development) test scenarios.
// Example:
//   Given I am on the login page
//   When I enter "admin" into the username field
//   Then I should see the dashboard

using Application.Features.NlpFeature.Dtos;
using Application.Setting;
using MediatR;
using Microsoft.AspNetCore.Http;

namespace Application.Features.NlpFeature.Commands
{
    /// <summary>
    /// Command to parse Gherkin text into structured steps.
    /// No database interaction needed — this is pure text processing.
    /// </summary>
    public record ParseGherkinCommand(string GherkinContent) : IRequest<ResponseHttp>
    {
        public class ParseGherkinCommandHandler : IRequestHandler<ParseGherkinCommand, ResponseHttp>
        {
            public Task<ResponseHttp> Handle(ParseGherkinCommand request, CancellationToken cancellationToken)
            {
                try
                {
                    // Validate that gherkin content is not empty
                    if (string.IsNullOrWhiteSpace(request.GherkinContent))
                    {
                        return Task.FromResult(new ResponseHttp
                        {
                            Status = StatusCodes.Status400BadRequest,
                            Fail_Messages = "Gherkin content cannot be empty."
                        });
                    }

                    // Parse the Gherkin text into individual steps
                    var parsedSteps = ParseGherkinText(request.GherkinContent);

                    // Build the response DTO
                    var response = new ParseGherkinResponseDto
                    {
                        IsValid = parsedSteps.Any(),
                        Steps = parsedSteps,
                        ErrorMessage = parsedSteps.Any() ? null : "No valid Gherkin steps found in the provided content."
                    };

                    return Task.FromResult(new ResponseHttp
                    {
                        Resultat = response,
                        Status = StatusCodes.Status200OK
                    });
                }
                catch (Exception ex)
                {
                    return Task.FromResult(new ResponseHttp
                    {
                        Fail_Messages = ex.Message,
                        Status = StatusCodes.Status400BadRequest
                    });
                }
            }

            /// <summary>
            /// Parses raw Gherkin text into a list of structured steps.
            /// Recognizes: Given, When, Then, And, But keywords.
            /// Lines that don't start with a keyword are ignored (comments, blank lines, Feature/Scenario headers).
            /// </summary>
            private List<ParsedStepDto> ParseGherkinText(string gherkinContent)
            {
                var steps = new List<ParsedStepDto>();

                // Gherkin keywords that start a step line
                var keywords = new[] { "Given", "When", "Then", "And", "But" };

                // Split the text into lines and process each one
                var lines = gherkinContent
                    .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(line => line.Trim()) // Remove leading/trailing whitespace
                    .Where(line => !string.IsNullOrEmpty(line)); // Skip empty lines

                foreach (var line in lines)
                {
                    // Check if this line starts with a Gherkin keyword
                    foreach (var keyword in keywords)
                    {
                        // Case-insensitive check: "given" or "Given" both work
                        if (line.StartsWith(keyword, StringComparison.OrdinalIgnoreCase))
                        {
                            steps.Add(new ParsedStepDto
                            {
                                // The keyword part (e.g., "Given")
                                Keyword = keyword,
                                // The text after the keyword (e.g., "I am on the login page")
                                Text = line.Substring(keyword.Length).Trim()
                            });
                            break; // Don't check other keywords for this line
                        }
                    }
                    // Lines that don't start with a keyword are ignored
                    // (e.g., "Feature:", "Scenario:", comments "#", etc.)
                }

                return steps;
            }
        }
    }
}
