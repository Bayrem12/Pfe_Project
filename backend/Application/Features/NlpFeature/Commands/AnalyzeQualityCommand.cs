// COMMAND: Analyze scenario quality BEFORE test execution.
// Endpoint: POST /api/nlp/analyze-quality
//
// FLOW:
// 1. Accept steps directly from the request body (no DB lookup needed — works before save).
// 2. Forward the payload to the Python IA Agent's /api/ia/analyze-quality endpoint.
// 3. Return the quality analysis result (score, issues, suggestions, improved steps).

using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Application.Setting;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace Application.Features.NlpFeature.Commands
{
    // ── DTOs sent in the request body ────────────────────────────────────────

    public class QualityStepDto
    {
        [JsonPropertyName("keyword")]
        public string Keyword { get; set; } = string.Empty;

        [JsonPropertyName("text")]
        public string Text { get; set; } = string.Empty;
    }

    // ── MediatR Command ───────────────────────────────────────────────────────

    public record AnalyzeQualityCommand(
        string ScenarioName,
        List<QualityStepDto> Steps,
        string Language = "en"
    ) : IRequest<ResponseHttp>
    {
        public class AnalyzeQualityCommandHandler : IRequestHandler<AnalyzeQualityCommand, ResponseHttp>
        {
            private readonly IHttpClientFactory _httpFactory;
            private readonly string _agentBaseUrl;

            private static readonly JsonSerializerOptions _jsonOptions = new()
            {
                PropertyNameCaseInsensitive = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            };

            public AnalyzeQualityCommandHandler(
                IHttpClientFactory httpFactory,
                IConfiguration configuration)
            {
                _httpFactory = httpFactory;
                _agentBaseUrl = (configuration["IAAgent:BaseUrl"] ?? "http://localhost:8000").TrimEnd('/');
            }

            public async Task<ResponseHttp> Handle(AnalyzeQualityCommand request, CancellationToken cancellationToken)
            {
                try
                {
                    var payload = new
                    {
                        scenario_name = request.ScenarioName,
                        steps = request.Steps.Select(s => new { keyword = s.Keyword, text = s.Text }),
                        language = request.Language,
                    };

                    var client = _httpFactory.CreateClient();
                    var agentUrl = $"{_agentBaseUrl}/api/ia/analyze-quality";

                    var response = await client.PostAsJsonAsync(agentUrl, payload, cancellationToken);

                    if (!response.IsSuccessStatusCode)
                    {
                        var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                        return new ResponseHttp
                        {
                            Status = StatusCodes.Status502BadGateway,
                            Fail_Messages = $"IA Agent returned {(int)response.StatusCode}: {errorBody}"
                        };
                    }

                    var analysisResult = await response.Content.ReadFromJsonAsync<JsonElement>(
                        cancellationToken: cancellationToken);

                    return new ResponseHttp
                    {
                        Status = StatusCodes.Status200OK,
                        Resultat = analysisResult,
                    };
                }
                catch (HttpRequestException httpEx)
                {
                    return new ResponseHttp
                    {
                        Status = StatusCodes.Status502BadGateway,
                        Fail_Messages = $"Could not reach IA Agent: {httpEx.Message}"
                    };
                }
                catch (Exception ex)
                {
                    return new ResponseHttp
                    {
                        Status = StatusCodes.Status500InternalServerError,
                        Fail_Messages = $"Unexpected error during quality analysis: {ex.Message}"
                    };
                }
            }
        }
    }
}
