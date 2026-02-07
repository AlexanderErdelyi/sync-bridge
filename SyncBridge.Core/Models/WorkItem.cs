namespace SyncBridge.Core.Models;

/// <summary>
/// Represents a work item (ticket, issue, task, etc.)
/// </summary>
public class WorkItem : SyncEntity
{
    /// <summary>
    /// Work item title
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Work item description
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Current state (e.g., New, Active, Resolved, Closed)
    /// </summary>
    public string State { get; set; } = string.Empty;

    /// <summary>
    /// Priority level
    /// </summary>
    public string? Priority { get; set; }

    /// <summary>
    /// Assigned to user
    /// </summary>
    public string? AssignedTo { get; set; }

    /// <summary>
    /// Work item type (e.g., Bug, Task, User Story)
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Comments associated with this work item
    /// </summary>
    public List<Comment> Comments { get; set; } = new();

    /// <summary>
    /// Custom fields specific to the system
    /// </summary>
    public Dictionary<string, object> CustomFields { get; set; } = new();
}
