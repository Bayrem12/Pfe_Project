using Domain.Common;

namespace Domain.Entities.TestData
{
    public class EnvironmentVariable : Entity
    {
        public Guid EnvironmentId { get; set; }
        public string Key { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public bool IsSecret { get; set; }

        // Navigation
        public Environment Environment { get; set; } = null!;
    }
}