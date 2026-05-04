// COMMAND: Analyze a scenario's steps using NLP via the IA Test Agent
// Endpoint: POST /api/Nlp/analyze/{scenarioId}
//
// FLOW:
// 1. Load the scenario + steps from the database.
// 2. Call the Python IA Test Agent's /api/ia/parse-scenario endpoint
//    (real NLP: spaCy + zero-shot transformer + sentence embeddings).
// 3. Map the agent's French intentions (naviguer, cliquer, …) to backend StepIntentType.
// 4. Persist StepAnalysis + StepParameter rows and return the DTOs.

using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Application.Features.NlpFeature.Dtos;
using Application.Interfaces;
using Application.Setting;
using AutoMapper;
using Domain.Entities.NLP;
using Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Application.Features.NlpFeature.Commands
{
    /// <summary>
    /// Command to analyze all steps of a scenario using the real NLP service
    /// hosted by the Python IA Test Agent.
    /// </summary>
    public record AnalyzeScenarioCommand(Guid ScenarioId) : IRequest<ResponseHttp>
    {
        public class AnalyzeScenarioCommandHandler : IRequestHandler<AnalyzeScenarioCommand, ResponseHttp>
        {
            private readonly IScenarioRepository _scenarioRepository;
            private readonly IStepAnalysisRepository _stepAnalysisRepository;
            private readonly IMapper _mapper;
            private readonly IHttpClientFactory _httpFactory;
            private readonly string _agentBaseUrl;

            public AnalyzeScenarioCommandHandler(
                IScenarioRepository scenarioRepository,
                IStepAnalysisRepository stepAnalysisRepository,
                IMapper mapper,
                IHttpClientFactory httpFactory,
                IConfiguration configuration)
            {
                _scenarioRepository = scenarioRepository;
                _stepAnalysisRepository = stepAnalysisRepository;
                _mapper = mapper;
                _httpFactory = httpFactory;
                _agentBaseUrl = (configuration["IAAgent:BaseUrl"] ?? "http://localhost:8000").TrimEnd('/');
            }

            public async Task<ResponseHttp> Handle(AnalyzeScenarioCommand request, CancellationToken cancellationToken)
            {
                try
                {
                    var scenario = await _scenarioRepository.GetWithStepsAsync(request.ScenarioId, cancellationToken);

                    if (scenario == null)
                    {
                        return new ResponseHttp
                        {
                            Status = StatusCodes.Status400BadRequest,
                            FailMessages = $"Scenario with ID {request.ScenarioId} not found."
                        };
                    }

                    var orderedSteps = scenario.Steps
                        .Where(s => !s.IsDeleted)
                        .OrderBy(s => s.DisplayOrder)
                        .ToList();

                    if (orderedSteps.Count == 0)
                    {
                        return new ResponseHttp
                        {
                            Status = StatusCodes.Status400BadRequest,
                            FailMessages = "Scenario has no steps to analyze."
                        };
                    }

                    // ── Build payload for the IA Agent ──
                    var agentRequest = new
                    {
                        scenario_name = scenario.Title,
                        steps = orderedSteps.Select(s => new
                        {
                            keyword = s.StepType.ToString(),
                            text = s.Text ?? string.Empty
                        }).ToList(),
                        language = "fr"
                    };

                    var http = _httpFactory.CreateClient();
                    http.Timeout = TimeSpan.FromMinutes(2);

                    AgentParseResponse? agentResponse;
                    try
                    {
                        var httpResponse = await http.PostAsJsonAsync(
                            $"{_agentBaseUrl}/api/ia/parse-scenario",
                            agentRequest,
                            cancellationToken);

                        httpResponse.EnsureSuccessStatusCode();
                        agentResponse = await httpResponse.Content.ReadFromJsonAsync<AgentParseResponse>(
                            cancellationToken: cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        return new ResponseHttp
                        {
                            Status = StatusCodes.Status502BadGateway,
                            FailMessages = $"Failed to reach IA Agent NLP service: {ex.Message}"
                        };
                    }

                    if (agentResponse?.StepsAnalyses == null || agentResponse.StepsAnalyses.Count == 0)
                    {
                        return new ResponseHttp
                        {
                            Status = StatusCodes.Status502BadGateway,
                            FailMessages = "IA Agent returned no analysis results."
                        };
                    }

                    // ── Map agent results → StepAnalysis entities ──
                    var analyses = new List<StepAnalysis>();
                    var pairCount = Math.Min(orderedSteps.Count, agentResponse.StepsAnalyses.Count);
                    for (int i = 0; i < pairCount; i++)
                    {
                        var step = orderedSteps[i];
                        var parsed = agentResponse.StepsAnalyses[i];

                        var (intent, action) = MapIntent(parsed.Intention);
                        var (target, value) = SplitEntities(parsed.Entites);

                        analyses.Add(new StepAnalysis
                        {
                            StepId = step.Id,
                            Intent = intent,
                            Action = action,
                            Target = target,
                            Value = value,
                            Confidence = parsed.Confiance,
                            AnalyzedAt = DateTime.UtcNow,
                            Parameters = (parsed.Entites ?? new List<AgentEntity>())
                                .Where(e => !string.IsNullOrWhiteSpace(e.Valeur))
                                .Select(e => new StepParameter
                                {
                                    Name = string.IsNullOrWhiteSpace(e.Nom) ? "param" : e.Nom,
                                    Value = e.Valeur,
                                    ParameterType = string.IsNullOrWhiteSpace(e.TypeEntite) ? "string" : e.TypeEntite
                                })
                                .ToList()
                        });
                    }

                    await _stepAnalysisRepository.PostRange(analyses);
                    await _stepAnalysisRepository.SaveChange(cancellationToken);

                    var dtos = _mapper.Map<List<StepAnalysisDto>>(analyses);

                    return new ResponseHttp
                    {
                        Resultat = dtos,
                        Status = StatusCodes.Status200OK
                    };
                }
                catch (Exception ex)
                {
                    return new ResponseHttp
                    {
                        FailMessages = ex.Message,
                        Status = StatusCodes.Status400BadRequest
                    };
                }
            }

            /// <summary>
            /// Map the agent's 18 French intent labels to the backend's 7-value enum.
            /// </summary>
            private static (StepIntentType Intent, string Action) MapIntent(string? agentIntent)
            {
                var intent = (agentIntent ?? string.Empty).Trim().ToLowerInvariant();
                return intent switch
                {
                    "naviguer" => (StepIntentType.Navigate, "navigate"),
                    "cliquer" or "soumettre_formulaire" or "changer_onglet" or "survol" or "scroller"
                        => (StepIntentType.Click, intent),
                    "saisir_texte" or "effacer" or "telecharger"
                        => (StepIntentType.Input, intent),
                    "selectionner" or "cocher" or "decocher"
                        => (StepIntentType.Select, intent),
                    "verifier_coche" or "verifier_texte" or "verifier_visibilite"
                        or "verifier_url" or "verifier_tableau"
                        => (StepIntentType.Assert, intent),
                    "attendre" => (StepIntentType.Wait, "wait"),
                    _ => (StepIntentType.Custom, string.IsNullOrWhiteSpace(intent) ? "custom" : intent),
                };
            }

            /// <summary>
            /// Pull a target (cible/element) and a value out of the agent entities list.
            /// </summary>
            private static (string Target, string? Value) SplitEntities(List<AgentEntity>? entities)
            {
                if (entities == null || entities.Count == 0)
                    return (string.Empty, null);

                string? target = entities.FirstOrDefault(e =>
                    e.Nom is "cible" or "url" or "type_element" or "identifier" or "element")?.Valeur;
                string? value = entities.FirstOrDefault(e =>
                    e.Nom is "valeur" or "value" or "texte")?.Valeur;

                target ??= entities.First().Valeur;
                return (target ?? string.Empty, value);
            }

            // ── DTOs matching the IA Agent's /api/ia/parse-scenario response ──

            private sealed class AgentParseResponse
            {
                [JsonPropertyName("scenario_name")] public string ScenarioName { get; set; } = string.Empty;
                [JsonPropertyName("nombre_steps")] public int NombreSteps { get; set; }
                [JsonPropertyName("steps_analyses")] public List<AgentParsedStep> StepsAnalyses { get; set; } = new();
                [JsonPropertyName("temps_traitement_ms")] public double TempsTraitementMs { get; set; }
            }

            private sealed class AgentParsedStep
            {
                [JsonPropertyName("step_type")] public string StepType { get; set; } = string.Empty;
                [JsonPropertyName("raw_text")] public string RawText { get; set; } = string.Empty;
                [JsonPropertyName("intention")] public string Intention { get; set; } = string.Empty;
                [JsonPropertyName("entites")] public List<AgentEntity> Entites { get; set; } = new();
                [JsonPropertyName("confiance")] public double Confiance { get; set; }
            }

            private sealed class AgentEntity
            {
                [JsonPropertyName("nom")] public string Nom { get; set; } = string.Empty;
                [JsonPropertyName("valeur")] public string Valeur { get; set; } = string.Empty;
                [JsonPropertyName("type_entite")] public string TypeEntite { get; set; } = string.Empty;
            }
        }
    }
}
