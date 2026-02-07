using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using SyncBridge.Adapters.AzureDevOps;
using SyncBridge.Adapters.Crm.Mock;
using SyncBridge.Adapters.ServiceDeskPlus;
using SyncBridge.Core.Configuration;
using SyncBridge.Core.Interfaces;
using SyncBridge.Core.Models;
using SyncBridge.Core.Services;
using System.Text.Json;

namespace SyncBridge.Web.Pages;

public class TestingModel : PageModel
{
    private readonly ILogger<TestingModel> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly SyncEngine _syncEngine;

    public TestingModel(
        ILogger<TestingModel> logger,
        ILoggerFactory loggerFactory,
        IHttpClientFactory httpClientFactory,
        SyncEngine syncEngine)
    {
        _logger = logger;
        _loggerFactory = loggerFactory;
        _httpClientFactory = httpClientFactory;
        _syncEngine = syncEngine;
    }

    public Dictionary<string, TestResult> ConnectionResults { get; set; } = new();
    public List<WorkItem> RetrievedItems { get; set; } = new();

    public void OnGet()
    {
    }

    private List<ISyncAdapter> GetConfiguredAdapters()
    {
        var adapters = new List<ISyncAdapter>();

        // Try to load Azure DevOps config from session
        var azureConfigJson = HttpContext.Session.GetString("AzureDevOpsConfig");
        AzureDevOpsConfig? azureConfig = null;
        if (!string.IsNullOrEmpty(azureConfigJson))
        {
            azureConfig = JsonSerializer.Deserialize<AzureDevOpsConfig>(azureConfigJson);
        }

        if (azureConfig != null)
        {
            var adapterConfig = new AdapterConfiguration { AzureDevOps = azureConfig };
            var options = Options.Create(adapterConfig);
            adapters.Add(new AzureDevOpsAdapter(
                _loggerFactory.CreateLogger<AzureDevOpsAdapter>(),
                options));
        }

        // Try to load ServiceDesk Plus config from session
        var serviceDeskJson = HttpContext.Session.GetString("ServiceDeskConfig");
        ServiceDeskPlusConfig? serviceDeskConfig = null;
        if (!string.IsNullOrEmpty(serviceDeskJson))
        {
            serviceDeskConfig = JsonSerializer.Deserialize<ServiceDeskPlusConfig>(serviceDeskJson);
        }

        if (serviceDeskConfig != null)
        {
            var adapterConfig = new AdapterConfiguration { ServiceDeskPlus = serviceDeskConfig };
            var options = Options.Create(adapterConfig);
            var httpClient = _httpClientFactory.CreateClient();
            adapters.Add(new ServiceDeskPlusAdapter(
                _loggerFactory.CreateLogger<ServiceDeskPlusAdapter>(),
                options,
                httpClient));
        }

        // Always add Mock CRM
        adapters.Add(new MockCrmAdapter(_loggerFactory.CreateLogger<MockCrmAdapter>()));

        return adapters;
    }

    public async Task<IActionResult> OnPostTestConnectionsAsync()
    {
        ConnectionResults = new Dictionary<string, TestResult>();
        var adapters = GetConfiguredAdapters();

        if (!adapters.Any())
        {
            TempData["Error"] = "No adapters configured. Please configure at least one adapter in the Configuration page.";
            return Page();
        }

        foreach (var adapter in adapters)
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
            var adapters = GetConfiguredAdapters();
            var adapter = adapters.FirstOrDefault(a => a.SystemName == adapterName);
            if (adapter == null)
            {
                TempData["Error"] = $"Adapter '{adapterName}' not found or not configured.";
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
            var adapters = GetConfiguredAdapters();
            
            if (adapters.Count < 2)
            {
                TempData["Error"] = "At least 2 adapters are required for sync testing. Please configure Azure DevOps and/or ServiceDesk Plus.";
                return RedirectToPage();
            }

            var sourceAdapter = adapters.First();
            var targetAdapter = adapters.Skip(1).First();

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
