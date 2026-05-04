using Domain.Enums;

namespace Application.Features.ScenariosFeature.DTOs
{
    public class ScenarioDto
    {
        public Guid Id { get; set; }
        public Guid FeatureId { get; set; }
        public string FeatureName { get; set; } = string.Empty;
        public string ModuleName { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public ScenarioStatus Status { get; set; }
        public int CurrentVersion { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public int StepCount { get; set; }
        public List<string> Tags { get; set; } = new();
        public string? LastTestStatus { get; set; }
    }

    public class ScenarioDetailDto
    {
        public Guid Id { get; set; }
        public Guid FeatureId { get; set; }
        public string FeatureName { get; set; } = string.Empty;
        public Guid? ModuleId { get; set; }
        public string ModuleName { get; set; } = string.Empty;
        public Guid? ProjectId { get; set; }
        public string ProjectName { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string GherkinContent { get; set; } = string.Empty;
        public ScenarioStatus Status { get; set; }
        public int CurrentVersion { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        public List<StepDto> Steps { get; set; } = new();
        public List<ScenarioVersionDto> Versions { get; set; } = new();
        public List<string> Tags { get; set; } = new();
        public string? LastTestStatus { get; set; }
    }

    public class StepDto
    {
        public Guid Id { get; set; }
        public StepType StepType { get; set; }
        public string Text { get; set; } = string.Empty;
        public int DisplayOrder { get; set; }
    }

    public class ScenarioVersionDto
    {
        public Guid Id { get; set; }
        public int VersionNumber { get; set; }
        public string GherkinContent { get; set; } = string.Empty;
        public string ChangeDescription { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }

    public class ValidationResultDto
    {
        public bool IsValid { get; set; }
        public List<string> Errors { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
    }
}