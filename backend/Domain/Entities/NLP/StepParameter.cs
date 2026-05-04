using Domain.Common;

namespace Domain.Entities.NLP
{
    public class StepParameter : Entity
    {
        public Guid StepAnalysisId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public string ParameterType { get; set; } = string.Empty;

        // Navigation
        public StepAnalysis StepAnalysis { get; set; } = null!;
    }
}