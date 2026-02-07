using SyncBridge.Core.Interfaces;
using SyncBridge.Core.Models;
using Microsoft.Extensions.Logging;

namespace SyncBridge.Core.Services;

/// <summary>
/// Core synchronization engine for bidirectional sync
/// </summary>
public class SyncEngine
{
    private readonly ILogger<SyncEngine> _logger;
    private readonly Dictionary<string, DateTime> _lastSyncTimes = new();

    public SyncEngine(ILogger<SyncEngine> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Performs bidirectional synchronization between two adapters
    /// </summary>
    public async Task<SyncResult> SyncBidirectional(
        ISyncAdapter sourceAdapter,
        ISyncAdapter targetAdapter,
        bool syncComments = true,
        CancellationToken cancellationToken = default)
    {
        var result = new SyncResult
        {
            StartTime = DateTime.UtcNow,
            SourceSystem = sourceAdapter.SystemName,
            TargetSystem = targetAdapter.SystemName
        };

        try
        {
            _logger.LogInformation("Starting bidirectional sync between {Source} and {Target}",
                sourceAdapter.SystemName, targetAdapter.SystemName);

            // Get last sync time for source
            var sourceLastSync = _lastSyncTimes.GetValueOrDefault(
                $"{sourceAdapter.SystemName}_{targetAdapter.SystemName}",
                DateTime.UtcNow.AddDays(-30));

            // Get changes from source
            var sourceChanges = await sourceAdapter.GetChanges(sourceLastSync, cancellationToken);
            
            // Sync source changes to target
            foreach (var change in sourceChanges)
            {
                try
                {
                    await SyncChange(change, targetAdapter, cancellationToken);
                    result.ItemsSynced++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to sync change {ChangeId} from {Source} to {Target}",
                        change.Entity.Id, sourceAdapter.SystemName, targetAdapter.SystemName);
                    result.Errors.Add($"Failed to sync {change.Entity.Id}: {ex.Message}");
                }
            }

            // Get last sync time for target
            var targetLastSync = _lastSyncTimes.GetValueOrDefault(
                $"{targetAdapter.SystemName}_{sourceAdapter.SystemName}",
                DateTime.UtcNow.AddDays(-30));

            // Get changes from target (for bidirectional sync)
            var targetChanges = await targetAdapter.GetChanges(targetLastSync, cancellationToken);
            
            // Sync target changes back to source
            foreach (var change in targetChanges)
            {
                try
                {
                    // Only sync back if the item wasn't originally from source
                    if (change.Entity.Source != sourceAdapter.SystemName)
                    {
                        await SyncChange(change, sourceAdapter, cancellationToken);
                        result.ItemsSynced++;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to sync change {ChangeId} from {Target} to {Source}",
                        change.Entity.Id, targetAdapter.SystemName, sourceAdapter.SystemName);
                    result.Errors.Add($"Failed to sync {change.Entity.Id}: {ex.Message}");
                }
            }

            // Update last sync times
            _lastSyncTimes[$"{sourceAdapter.SystemName}_{targetAdapter.SystemName}"] = DateTime.UtcNow;
            _lastSyncTimes[$"{targetAdapter.SystemName}_{sourceAdapter.SystemName}"] = DateTime.UtcNow;

            result.Success = result.Errors.Count == 0;
            result.EndTime = DateTime.UtcNow;

            _logger.LogInformation("Sync completed: {ItemsSynced} items synced, {Errors} errors",
                result.ItemsSynced, result.Errors.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fatal error during sync");
            result.Success = false;
            result.Errors.Add($"Fatal error: {ex.Message}");
            result.EndTime = DateTime.UtcNow;
        }

        return result;
    }

    private async Task SyncChange(SyncChange change, ISyncAdapter targetAdapter, CancellationToken cancellationToken)
    {
        switch (change.ChangeType)
        {
            case ChangeType.Created:
            case ChangeType.Updated:
                // For work items, also sync comments if present
                if (change.Entity is WorkItem workItem && workItem.Comments.Any())
                {
                    _logger.LogDebug("Syncing work item {Id} with {CommentCount} comments",
                        workItem.Id, workItem.Comments.Count);
                }
                await targetAdapter.Upsert(change.Entity, cancellationToken);
                break;
            case ChangeType.Deleted:
                _logger.LogWarning("Delete synchronization not yet implemented for {Id}", change.Entity.Id);
                break;
        }
    }
}

/// <summary>
/// Result of a sync operation
/// </summary>
public class SyncResult
{
    public bool Success { get; set; }
    public string SourceSystem { get; set; } = string.Empty;
    public string TargetSystem { get; set; } = string.Empty;
    public int ItemsSynced { get; set; }
    public List<string> Errors { get; set; } = new();
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public TimeSpan Duration => EndTime - StartTime;
}
