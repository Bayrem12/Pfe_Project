namespace Application.Features.NlpFeature.Dtos
{
    /// <summary>
    /// Request body model for the CreateActionMapping endpoint.
    /// We need this separate class because the projectId comes from the URL path,
    /// not from the JSON body. The command combines both.
    /// </summary>
    public class CreateActionMappingRequest
    {
        public string IntentPattern { get; set; } = string.Empty;
        public string ActionType { get; set; } = string.Empty;
        public string SelectorStrategy { get; set; } = string.Empty;
        public string SelectorValue { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        /// <summary>
        /// Higher value = evaluated first when multiple active mappings could match.
        /// Default 0 is fine; raise it (e.g. 10, 20) for more-specific patterns.
        /// </summary>
        public int Priority { get; set; } = 0;
    }
}
