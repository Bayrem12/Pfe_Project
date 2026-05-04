using Domain.Common;
using Domain.Entities.NLP;
using Domain.Enums;

namespace Domain.Entities.Scenarios
{
    public class Step : Entity
    {
        public Guid ScenarioId { get; set; }
        public Scenario Scenario { get; set; } = null!;
        public StepType StepType { get; set; }
        public string Text { get; set; } = default!;
        public int DisplayOrder { get; set; }
        public string? DataTable { get; set; }

        public ICollection<StepParameter> Parameters { get; set; } = new List<StepParameter>();

    }
}