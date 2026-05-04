using Domain.Enums;

namespace Application.Features.ScenariosFeature.DTOs
{
    public record UpdateScenarioRequest(string Title, string Description, string GherkinContent, string? ChangeDescription, ScenarioStatus? Status = null);
}
