using Domain.Enums;

namespace Application.Features.ScenariosFeature.DTOs
{
    public record CreateScenarioRequest(Guid FeatureId, string Title, string Description, string GherkinContent, ScenarioStatus Status = ScenarioStatus.Draft, List<string>? Tags = null);
}
