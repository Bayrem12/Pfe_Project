using Domain.Common;
using Domain.Entities.ProjectManagement;
using Domain.Enums;

namespace Domain.Entities.NLP
{
    public class ActionMapping : Entity
    {
        public Guid ProjectId { get; set; }
        public string IntentPattern { get; set; } = string.Empty;
        public UIActionType ActionType { get; set; }
        public string SelectorStrategy { get; set; } = string.Empty;
        public string SelectorValue { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
        /// <summary>
        /// Higher value = evaluated first. Lets admins make specific patterns
        /// take precedence over broad ones without relying on alphabetical order.
        /// </summary>
        public int Priority { get; set; } = 0;

        // Navigation
        public Project Project { get; set; } = null!;
    }
}