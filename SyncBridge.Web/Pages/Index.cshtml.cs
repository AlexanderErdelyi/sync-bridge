using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using SyncBridge.Core.Configuration;
using SyncBridge.Core.Interfaces;
using SyncBridge.Core.Services;

namespace SyncBridge.Web.Pages;

public class IndexModel : PageModel
{
    private readonly ILogger<IndexModel> _logger;
    private readonly IEnumerable<ISyncAdapter> _adapters;
    private readonly SyncEngine _syncEngine;
    private readonly IOptions<SyncConfiguration> _syncConfig;

    public IndexModel(
        ILogger<IndexModel> logger,
        IEnumerable<ISyncAdapter> adapters,
        SyncEngine syncEngine,
        IOptions<SyncConfiguration> syncConfig)
    {
        _logger = logger;
        _adapters = adapters;
        _syncEngine = syncEngine;
        _syncConfig = syncConfig;
    }

    public IEnumerable<ISyncAdapter> Adapters => _adapters;
    public int AdapterCount => _adapters.Count();
    public int MappingCount => _syncConfig.Value.Mappings.Count;
    public SyncResult? LastSyncResult { get; set; }

    public void OnGet()
    {
        // Load last sync result from session if available
        var lastSyncJson = HttpContext.Session.GetString("LastSyncResult");
        if (!string.IsNullOrEmpty(lastSyncJson))
        {
            LastSyncResult = System.Text.Json.JsonSerializer.Deserialize<SyncResult>(lastSyncJson);
        }
    }

    public async Task<IActionResult> OnPostManualSyncAsync()
    {
        try
        {
            var config = _syncConfig.Value;
            if (!config.Mappings.Any())
            {
                TempData["Error"] = "No sync mappings configured. Please configure sync mappings first.";
                return RedirectToPage();
            }

            var mapping = config.Mappings.First();
            var sourceAdapter = _adapters.FirstOrDefault(a => a.SystemName == mapping.SourceSystem);
            var targetAdapter = _adapters.FirstOrDefault(a => a.SystemName == mapping.TargetSystem);

            if (sourceAdapter == null || targetAdapter == null)
            {
                TempData["Error"] = "Could not find configured adapters. Please check your configuration.";
                return RedirectToPage();
            }

            // Initialize adapters
            await sourceAdapter.Initialize(CancellationToken.None);
            await targetAdapter.Initialize(CancellationToken.None);

            // Perform sync
            var result = await _syncEngine.SyncBidirectional(
                sourceAdapter,
                targetAdapter,
                config.SyncComments,
                CancellationToken.None);

            // Store result in session
            var resultJson = System.Text.Json.JsonSerializer.Serialize(result);
            HttpContext.Session.SetString("LastSyncResult", resultJson);

            TempData["Success"] = $"Sync completed: {result.ItemsSynced} items synced";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during manual sync");
            TempData["Error"] = $"Sync failed: {ex.Message}";
        }

        return RedirectToPage();
    }
}
