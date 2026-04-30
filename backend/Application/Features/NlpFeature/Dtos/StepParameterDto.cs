// DTO (Data Transfer Object) for StepParameter
// DTOs control exactly what data we send back to the Angular frontend.
// We don't expose the full Entity (which has audit fields like IsDeleted, CreatedBy, etc.)

namespace Application.Features.NlpFeature.Dtos
{
    /// <summary>
    /// Represents a single parameter extracted from an NLP-analyzed step.
    /// Example: for "Type 'hello' into #username", Name="value", Value="hello", ParameterType="string"
    /// </summary>
    public class StepParameterDto
    {
        public string Name { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public string ParameterType { get; set; } = string.Empty;
    }
}
