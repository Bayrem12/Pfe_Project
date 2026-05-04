namespace Application.Features.FeatureFeature.Dtos
{
    public class UpdateFeatureRequest
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int DisplayOrder { get; set; }
    }
}
