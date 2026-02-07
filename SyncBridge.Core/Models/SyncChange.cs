namespace SyncBridge.Core.Models;

/// <summary>
/// Represents a change detected during synchronization
/// </summary>
public class SyncChange
{
    /// <summary>
    /// Entity that changed
    /// </summary>
    public SyncEntity Entity { get; set; } = null!;

    /// <summary>
    /// Type of change
    /// </summary>
    public ChangeType ChangeType { get; set; }

    /// <summary>
    /// When the change occurred
    /// </summary>
    public DateTime Timestamp { get; set; }
}

/// <summary>
/// Types of changes that can occur
/// </summary>
public enum ChangeType
{
    Created,
    Updated,
    Deleted
}
