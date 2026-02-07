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

public class WorkItemsModel : PageModel
{
    private readonly ILogger<WorkItemsModel> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly SyncEngine _syncEngine;

    public WorkItemsModel(
        ILogger<WorkItemsModel> logger,
        ILoggerFactory loggerFactory,
        IHttpClientFactory httpClientFactory,
        SyncEngine syncEngine)
    {
        _logger = logger;
        _loggerFactory = loggerFactory;
        _httpClientFactory = httpClientFactory;
        _syncEngine = syncEngine;
    }

    public List<ISyncAdapter> Adapters { get; set; } = new();
    public List<WorkItem> WorkItems { get; set; } = new();

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

    public async Task OnGetAsync()
    {
        Adapters = GetConfiguredAdapters();

        // Load work items from session
        var workItemsJson = HttpContext.Session.GetString("WorkItems");
        if (!string.IsNullOrEmpty(workItemsJson))
        {
            WorkItems = JsonSerializer.Deserialize<List<WorkItem>>(workItemsJson) ?? new();
        }
        else
        {
            // If no items in session, try to load from the first adapter
            try
            {
                var adapter = Adapters.FirstOrDefault();
                if (adapter != null)
                {
                    await adapter.Initialize(CancellationToken.None);
                    var changes = await adapter.GetChanges(DateTime.UtcNow.AddDays(-30), CancellationToken.None);
                    WorkItems = changes
                        .Where(c => c.Entity is WorkItem)
                        .Select(c => c.Entity as WorkItem)
                        .Where(w => w != null)
                        .Cast<WorkItem>()
                        .Take(20)
                        .ToList();

                    // Store in session
                    var json = JsonSerializer.Serialize(WorkItems);
                    HttpContext.Session.SetString("WorkItems", json);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading work items");
            }
        }
    }

    public async Task<IActionResult> OnPostCreateWorkItemAsync(
        string targetSystem,
        string title,
        string description,
        string type,
        string priority,
        string state,
        string assignedTo,
        bool syncToDevOps)
    {
        try
        {
            var adapters = GetConfiguredAdapters();
            var adapter = adapters.FirstOrDefault(a => a.SystemName == targetSystem);
            if (adapter == null)
            {
                TempData["Error"] = $"Adapter '{targetSystem}' not found or not configured.";
                return RedirectToPage();
            }

            _logger.LogInformation("Creating work item in {TargetSystem}", targetSystem);

            // Create the work item
            var workItem = new WorkItem
            {
                Id = Guid.NewGuid().ToString(),
                Title = title,
                Description = description,
                Type = type,
                Priority = priority,
                State = state,
                AssignedTo = assignedTo,
                Source = targetSystem,
                LastModified = DateTime.UtcNow
            };

            // Initialize adapter and create the work item
            await adapter.Initialize(CancellationToken.None);
            var createdItem = await adapter.Upsert(workItem, CancellationToken.None);

            _logger.LogInformation("Work item created with ID: {WorkItemId}", createdItem.Id);

            // Set ExternalId for cross-system tracking
            if (createdItem is WorkItem createdWorkItem && string.IsNullOrEmpty(createdWorkItem.ExternalId))
            {
                createdWorkItem.ExternalId = $"{targetSystem}:{createdWorkItem.Id}";
            }

            // If sync is enabled, sync to other systems
            if (syncToDevOps && adapters.Count > 1)
            {
                var otherAdapters = adapters.Where(a => a.SystemName != targetSystem).ToList();
                foreach (var otherAdapter in otherAdapters)
                {
                    try
                    {
                        await otherAdapter.Initialize(CancellationToken.None);
                        var syncedItem = await otherAdapter.Upsert(createdItem, CancellationToken.None);
                        _logger.LogInformation("Work item synced to {System} with ID: {Id}",
                            otherAdapter.SystemName, syncedItem.Id);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error syncing to {System}", otherAdapter.SystemName);
                    }
                }
            }

            // Add to session storage
            var workItemsJson = HttpContext.Session.GetString("WorkItems");
            var workItems = string.IsNullOrEmpty(workItemsJson)
                ? new List<WorkItem>()
                : JsonSerializer.Deserialize<List<WorkItem>>(workItemsJson) ?? new();

            workItems.Insert(0, createdItem as WorkItem ?? workItem);
            HttpContext.Session.SetString("WorkItems", JsonSerializer.Serialize(workItems));

            TempData["Success"] = syncToDevOps
                ? $"Work item created in {targetSystem} and synced to other systems!"
                : $"Work item created in {targetSystem}!";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating work item");
            TempData["Error"] = $"Error creating work item: {ex.Message}";
        }

        return RedirectToPage();
    }
}
