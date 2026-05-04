// COMMAND: Analyze a single failed step AFTER test execution.
// Endpoint: POST /api/nlp/analyze-failure
//
// FLOW:
// 1. Accept the failed step's data (text, error message, selector, …) directly.
// 2. Forward the payload to the Python IA Agent's /api/ia/analyze-failure endpoint.
// 3. Return the failure analysis (category, root cause, explanation, fix, confidence).

using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Application.Setting;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace Application.Features.NlpFeature.Commands
{
    public record AnalyzeFailureCommand(
        string StepText,
        string ErrorMessage,
        string Selector = "",
        string Keyword = "",
        bool VisualFallbackUsed = false,
        int RetryCount = 0
    ) : IRequest<ResponseHttp>
    {
        public class AnalyzeFailureCommandHandler : IRequestHandler<AnalyzeFailureCommand, ResponseHttp>
        {
            private readonly IHttpClientFactory _httpFactory;
            private readonly string _agentBaseUrl;

            private static readonly JsonSerializerOptions _jsonOptions = new()
            {
                PropertyNameCaseInsensitive = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            };

            public AnalyzeFailureCommandHandler(
                IHttpClientFactory httpFactory,
                IConfiguration configuration)
            {
                _httpFactory = httpFactory;
                _agentBaseUrl = (configuration["IAAgent:BaseUrl"] ?? "http://localhost:8000").TrimEnd('/');
            }

            public async Task<ResponseHttp> Handle(AnalyzeFailureCommand request, CancellationToken cancellationToken)
            {
                try
                {
                    var payload = new
                    {
                        step_text = request.StepText,
                        error_message = request.ErrorMessage,
                        selector = request.Selector ?? string.Empty,
                        keyword = request.Keyword ?? string.Empty,
                        visual_fallback_used = request.VisualFallbackUsed,
                        retry_count = request.RetryCount,
                    };

                    var client = _httpFactory.CreateClient();
                    var agentUrl = $"{_agentBaseUrl}/api/ia/analyze-failure";

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
                        Fail_Messages = $"Unexpected error during failure analysis: {ex.Message}"
                    };
                }
            }
        }
    }
}
