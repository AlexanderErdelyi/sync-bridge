using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SyncBridge.Core.Configuration;
using SyncBridge.Core.Interfaces;

namespace SyncBridge.Core.Services;

/// <summary>
/// Background service for automatic synchronization
/// </summary>
public class SyncBackgroundService : BackgroundService
{
    private readonly ILogger<SyncBackgroundService> _logger;
    private readonly SyncEngine _syncEngine;
    private readonly IEnumerable<ISyncAdapter> _adapters;
    private readonly SyncConfiguration _config;

    public SyncBackgroundService(
        ILogger<SyncBackgroundService> logger,
        SyncEngine syncEngine,
        IEnumerable<ISyncAdapter> adapters,
        IOptions<SyncConfiguration> config)
    {
        _logger = logger;
        _syncEngine = syncEngine;
        _adapters = adapters;
        _config = config.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Sync Background Service starting...");

        // Initialize all adapters
        foreach (var adapter in _adapters)
        {
            try
            {
                await adapter.Initialize(stoppingToken);
                _logger.LogInformation("Initialized adapter: {Adapter}", adapter.SystemName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize adapter: {Adapter}", adapter.SystemName);
            }
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PerformSync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during sync cycle");
            }

            // Wait for next poll interval
            await Task.Delay(TimeSpan.FromSeconds(_config.PollIntervalSeconds), stoppingToken);
        }

        _logger.LogInformation("Sync Background Service stopping...");
    }

    private async Task PerformSync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting sync cycle...");

        foreach (var mapping in _config.Mappings)
        {
            var sourceAdapter = _adapters.FirstOrDefault(a => a.SystemName == mapping.SourceSystem);
            var targetAdapter = _adapters.FirstOrDefault(a => a.SystemName == mapping.TargetSystem);

            if (sourceAdapter == null)
            {
                _logger.LogWarning("Source adapter not found: {Source}", mapping.SourceSystem);
                continue;
            }

            if (targetAdapter == null)
            {
                _logger.LogWarning("Target adapter not found: {Target}", mapping.TargetSystem);
                continue;
            }

            try
            {
                var result = await _syncEngine.SyncBidirectional(
                    sourceAdapter,
                    targetAdapter,
                    _config.SyncComments,
                    cancellationToken);

                if (result.Success)
                {
                    _logger.LogInformation("Sync completed: {Source} <-> {Target}, {Count} items synced in {Duration}",
                        result.SourceSystem, result.TargetSystem, result.ItemsSynced, result.Duration);
                }
                else
                {
                    _logger.LogWarning("Sync completed with errors: {Source} <-> {Target}, {ErrorCount} errors",
                        result.SourceSystem, result.TargetSystem, result.Errors.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to sync {Source} <-> {Target}",
                    mapping.SourceSystem, mapping.TargetSystem);
            }
        }

        _logger.LogInformation("Sync cycle completed");
    }
}
