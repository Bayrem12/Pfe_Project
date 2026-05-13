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
using Microsoft.Extensions.Logging;

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
            private readonly ILogger<AnalyzeQualityCommandHandler> _logger;

            private static readonly JsonSerializerOptions _jsonOptions = new()
            {
                PropertyNameCaseInsensitive = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            };

            public AnalyzeQualityCommandHandler(
                IHttpClientFactory httpFactory,
                IConfiguration configuration,
                ILogger<AnalyzeQualityCommandHandler> logger)
            {
                _httpFactory = httpFactory;
                _agentBaseUrl = (configuration["IAAgent:BaseUrl"] ?? "http://localhost:8000").TrimEnd('/');
                _logger = logger;
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

                    // Quality analysis may warm up SBERT + zero-shot models on first call — allow up to 5 minutes.
                    var client = _httpFactory.CreateClient();
                    client.Timeout = TimeSpan.FromMinutes(5);
                    var agentUrl = $"{_agentBaseUrl}/api/ia/analyze-quality";

                    var response = await client.PostAsJsonAsync(agentUrl, payload, cancellationToken);

                    if (!response.IsSuccessStatusCode)
                    {
                        var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                        _logger.LogError("IA Agent quality analysis returned {StatusCode}: {Body}", (int)response.StatusCode, errorBody);
                        return new ResponseHttp
                        {
                            Status = StatusCodes.Status502BadGateway,
                            FailMessages = "The quality analysis service is temporarily unavailable."
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
                    _logger.LogError(httpEx, "Could not reach IA Agent for quality analysis.");
                    return new ResponseHttp
                    {
                        Status = StatusCodes.Status502BadGateway,
                        FailMessages = "The quality analysis service is temporarily unavailable."
                    };
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unexpected error during quality analysis.");
                    return new ResponseHttp
                    {
                        Status = StatusCodes.Status500InternalServerError,
                        FailMessages = "An unexpected error occurred during quality analysis."
                    };
                }
            }
        }
    }
}
