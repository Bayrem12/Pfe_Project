using Domain.Common;
using Domain.Entities.Scenarios;
using Domain.Enums;

namespace Domain.Entities.NLP
{
    public class StepAnalysis : Entity
    {
        public Guid StepId { get; set; }
        public StepIntentType Intent { get; set; }
        public string Action { get; set; } = string.Empty;
        public string Target { get; set; } = string.Empty;
        public string? Value { get; set; }
        public double Confidence { get; set; }
        public DateTime AnalyzedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public Step Step { get; set; } = null!;
        public ICollection<StepParameter> Parameters { get; set; } = new List<StepParameter>();
    }

}
