using Domain.Common;

namespace Domain.Entities.TestData
{
    public class TestDataEntry : Entity
    {
        public Guid DataSetId { get; set; }
        public string Key { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public string DataType { get; set; } = "string";

        // Navigation
        public TestDataSet DataSet { get; set; } = null!;
    }
}