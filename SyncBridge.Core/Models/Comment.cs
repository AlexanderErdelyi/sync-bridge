namespace SyncBridge.Core.Models;

/// <summary>
/// Represents a comment on a work item
/// </summary>
public class Comment : SyncEntity
{
    /// <summary>
    /// Comment text content
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Author of the comment
    /// </summary>
    public string Author { get; set; } = string.Empty;

    /// <summary>
    /// When the comment was created
    /// </summary>
    public DateTime CreatedDate { get; set; }

    /// <summary>
    /// Parent work item ID
    /// </summary>
    public string WorkItemId { get; set; } = string.Empty;
}
