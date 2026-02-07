namespace SyncBridge.Core.Models;

/// <summary>
/// Base class for all synchronizable entities
/// </summary>
public class SyncEntity
{
    /// <summary>
    /// Unique identifier in the source system
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// External ID used for cross-system mapping
    /// </summary>
    public string? ExternalId { get; set; }

    /// <summary>
    /// When the entity was last modified
    /// </summary>
    public DateTime LastModified { get; set; }

    /// <summary>
    /// Source system identifier
    /// </summary>
    public string Source { get; set; } = string.Empty;
}
