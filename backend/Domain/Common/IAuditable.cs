using System;

namespace Domain.Common
{
    /// <summary>
    /// Auditable Interface
    /// </summary>
    public interface IAuditable
    {
        /// <summary>
        /// Gets or sets the created date.
        /// </summary>
        /// <value>
        /// The created date.
        /// </value>
        DateTime? CreatedDate { get; set; }

        /// <summary>
        /// Gets or sets the created by.
        /// </summary>
        /// <value>
        /// The created by.
        /// </value>
        string? CreatedBy { get; set; }

        /// <summary>
        /// Gets or sets the created by identifier.
        /// </summary>
        /// <value>
        /// The created by identifier.
        /// </value>
        string? CreatedById { get; set; }

        /// <summary>
        /// Gets or sets the modified date.
        /// </summary>
        /// <value>
        /// The modified date.
        /// </value>
        DateTime? ModifiedDate { get; set; }

        /// <summary>
        /// Gets or sets the modified by.
        /// </summary>
        /// <value>
        /// The modified by.
        /// </value>
        string? ModifiedBy { get; set; }

        /// <summary>
        /// Gets or sets the modified by identifier.
        /// </summary>
        /// <value>
        /// The modified by identifier.
        /// </value>
        string? ModifiedById { get; set; }
    }
}
