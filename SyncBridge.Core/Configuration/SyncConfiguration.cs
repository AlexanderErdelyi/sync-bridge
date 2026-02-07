namespace SyncBridge.Core.Configuration;

/// <summary>
/// Configuration for sync operations
/// </summary>
public class SyncConfiguration
{
    /// <summary>
    /// How often to poll for changes (in seconds)
    /// </summary>
    public int PollIntervalSeconds { get; set; } = 60;

    /// <summary>
    /// Maximum number of items to sync in a single batch
    /// </summary>
    public int BatchSize { get; set; } = 100;

    /// <summary>
    /// Whether to sync comments
    /// </summary>
    public bool SyncComments { get; set; } = true;

    /// <summary>
    /// Sync mappings between systems
    /// </summary>
    public List<SyncMapping> Mappings { get; set; } = new();
}

/// <summary>
/// Mapping configuration between two systems
/// </summary>
public class SyncMapping
{
    /// <summary>
    /// Source system name
    /// </summary>
    public string SourceSystem { get; set; } = string.Empty;

    /// <summary>
    /// Target system name
    /// </summary>
    public string TargetSystem { get; set; } = string.Empty;

    /// <summary>
    /// Whether sync is bidirectional
    /// </summary>
    public bool Bidirectional { get; set; } = true;

    /// <summary>
    /// Field mappings between systems
    /// </summary>
    public Dictionary<string, string> FieldMappings { get; set; } = new();
}
