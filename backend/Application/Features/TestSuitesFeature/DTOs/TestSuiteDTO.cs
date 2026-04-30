namespace Application.Features.TestSuitesFeature.DTOs
{
    public class TestSuiteDTO
    {
        public Guid Id { get; set; }
        public Guid ProjectId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public Guid CreatedById { get; set; }
        public DateTime? CreatedDate { get; set; }
        public DateTime? ModifiedDate { get; set; }
        public int ScenarioCount { get; set; }
    }

    public class TestSuiteWithCasesDTO
    {
        public Guid Id { get; set; }
        public Guid ProjectId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public Guid CreatedById { get; set; }
        public DateTime? CreatedDate { get; set; }
        public DateTime? ModifiedDate { get; set; }
        public List<TestSuiteScenarioDTO> Scenarios { get; set; } = new();
    }

    public class TestSuiteScenarioDTO
    {
        public Guid Id { get; set; }
        public Guid ScenarioId { get; set; }
        public string ScenarioTitle { get; set; } = string.Empty;
        public string ScenarioDescription { get; set; } = string.Empty;
        public int DisplayOrder { get; set; }
    }
}
