namespace Application.Features.TagsFeature.DTOs
{
    public class TagDTO
    {
        public Guid Id { get; set; }
        public Guid ProjectId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Color { get; set; }
        public string? Description { get; set; }
    }
}
