using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SyncBridge.Core.Interfaces;
using SyncBridge.Core.Models;
using SyncBridge.Core.Services;

namespace SyncBridge.Web.Pages;

public class TestingModel : PageModel
{
    private readonly ILogger<TestingModel> _logger;
    private readonly IEnumerable<ISyncAdapter> _adapters;
    private readonly SyncEngine _syncEngine;

    public TestingModel(
        ILogger<TestingModel> logger,
        IEnumerable<ISyncAdapter> adapters,
        SyncEngine syncEngine)
    {
        _logger = logger;
        _adapters = adapters;
        _syncEngine = syncEngine;
    }

    public IEnumerable<ISyncAdapter> Adapters => _adapters;
    public Dictionary<string, TestResult> ConnectionResults { get; set; } = new();
    public List<WorkItem> RetrievedItems { get; set; } = new();

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostTestConnectionsAsync()
    {
        ConnectionResults = new Dictionary<string, TestResult>();

        foreach (var adapter in _adapters)
        {
            try
            {
                _logger.LogInformation("Testing connection to {AdapterName}", adapter.SystemName);
                await adapter.Initialize(CancellationToken.None);

                // Try to get recent changes as a connection test
                var changes = await adapter.GetChanges(DateTime.UtcNow.AddDays(-1), CancellationToken.None);

                ConnectionResults[adapter.SystemName] = new TestResult
                {
                    Success = true,
                    Message = $"Connected successfully. Found {changes.Count()} recent changes."
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to connect to {AdapterName}", adapter.SystemName);
                ConnectionResults[adapter.SystemName] = new TestResult
                {
                    Success = false,
                    Message = $"Connection failed: {ex.Message}"
                };
            }
        }

        TempData["Success"] = "Connection tests completed.";
        return Page();
    }

    public async Task<IActionResult> OnPostRetrieveDataAsync(string adapterName)
    {
        try
        {
            var adapter = _adapters.FirstOrDefault(a => a.SystemName == adapterName);
            if (adapter == null)
            {
                TempData["Error"] = $"Adapter '{adapterName}' not found.";
                return RedirectToPage();
            }

            _logger.LogInformation("Retrieving data from {AdapterName}", adapterName);

            await adapter.Initialize(CancellationToken.None);

            // Get changes from the last 30 days
            var changes = await adapter.GetChanges(DateTime.UtcNow.AddDays(-30), CancellationToken.None);

            RetrievedItems = changes
                .Where(c => c.Entity is WorkItem)
                .Select(c => c.Entity as WorkItem)
                .Where(w => w != null)
                .Cast<WorkItem>()
                .ToList();

            TempData["Success"] = $"Retrieved {RetrievedItems.Count} work items from {adapterName}.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving data from {AdapterName}", adapterName);
            TempData["Error"] = $"Error retrieving data: {ex.Message}";
        }

        return Page();
    }

    public async Task<IActionResult> OnPostTestSyncAsync()
    {
        try
        {
            if (_adapters.Count() < 2)
            {
                TempData["Error"] = "At least 2 adapters are required for sync testing.";
                return RedirectToPage();
            }

            var sourceAdapter = _adapters.First();
            var targetAdapter = _adapters.Skip(1).First();

            _logger.LogInformation("Testing sync between {Source} and {Target}",
                sourceAdapter.SystemName, targetAdapter.SystemName);

            await sourceAdapter.Initialize(CancellationToken.None);
            await targetAdapter.Initialize(CancellationToken.None);

            var result = await _syncEngine.SyncBidirectional(
                sourceAdapter,
                targetAdapter,
                syncComments: true,
                CancellationToken.None);

            if (result.Success)
            {
                TempData["Success"] = $"Test sync completed successfully! {result.ItemsSynced} items synced in {result.Duration.TotalSeconds:F2}s";
            }
            else
            {
                TempData["Error"] = $"Test sync completed with errors: {string.Join(", ", result.Errors)}";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during test sync");
            TempData["Error"] = $"Test sync failed: {ex.Message}";
        }

        return RedirectToPage();
    }

    public class TestResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}
