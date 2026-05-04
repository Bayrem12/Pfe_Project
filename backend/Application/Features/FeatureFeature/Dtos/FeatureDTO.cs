namespace Application.Features.FeatureFeature.Dtos
{
    public class FeatureDTO
    {
        public Guid Id { get; set; }
        public Guid ModuleId { get; set; }
        public string ModuleName { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int DisplayOrder { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public int ScenarioCount { get; set; }
    }

    public class FeatureListDTO
    {
        public Guid Id { get; set; }
        public Guid ModuleId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int DisplayOrder { get; set; }
        public int ScenarioCount { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
