using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using SyncBridge.Adapters.AzureDevOps;
using SyncBridge.Adapters.Crm.Mock;
using SyncBridge.Adapters.ServiceDeskPlus;
using SyncBridge.Core.Configuration;
using SyncBridge.Core.Helpers;
using SyncBridge.Core.Interfaces;
using SyncBridge.Core.Models;
using System.Text.Json;

namespace SyncBridge.Web.Pages;

public class WorkItemDetailsModel : PageModel
{
    private readonly ILogger<WorkItemDetailsModel> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IHttpClientFactory _httpClientFactory;

    public WorkItemDetailsModel(
        ILogger<WorkItemDetailsModel> logger,
        ILoggerFactory loggerFactory,
        IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _loggerFactory = loggerFactory;
        _httpClientFactory = httpClientFactory;
    }

    public WorkItem? WorkItem { get; set; }

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

    public async Task<IActionResult> OnGetAsync(string id, string system)
    {
        try
        {
            // First try to load from session
            var workItemsJson = HttpContext.Session.GetString("WorkItems");
            if (!string.IsNullOrEmpty(workItemsJson))
            {
                var workItems = JsonSerializer.Deserialize<List<WorkItem>>(workItemsJson);
                WorkItem = workItems?.FirstOrDefault(w => w.Id == id);
            }

            // If not in session, try to fetch from the adapter
            if (WorkItem == null)
            {
                var adapters = GetConfiguredAdapters();
                var adapter = adapters.FirstOrDefault(a => a.SystemName == system);
                if (adapter != null)
                {
                    await adapter.Initialize(CancellationToken.None);
                    var entity = await adapter.GetById(id, CancellationToken.None);
                    WorkItem = entity as WorkItem;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading work item {Id} from {System}", id, system);
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAddCommentAsync(
        string workItemId,
        string systemName,
        string author,
        string text,
        bool syncComment)
    {
        try
        {
            _logger.LogInformation("Adding comment to work item {WorkItemId} in {System}",
                workItemId, systemName);

            var adapters = GetConfiguredAdapters();
            var adapter = adapters.FirstOrDefault(a => a.SystemName == systemName);
            if (adapter == null)
            {
                TempData["Error"] = $"Adapter '{systemName}' not found or not configured.";
                return RedirectToPage(new { id = workItemId, system = systemName });
            }

            await adapter.Initialize(CancellationToken.None);

            // Get the work item
            var entity = await adapter.GetById(workItemId, CancellationToken.None);
            var workItem = entity as WorkItem;

            if (workItem == null)
            {
                TempData["Error"] = "Work item not found.";
                return RedirectToPage(new { id = workItemId, system = systemName });
            }

            // Create the comment
            var comment = new Comment
            {
                Id = Guid.NewGuid().ToString(),
                Text = text,
                Author = author,
                CreatedDate = DateTime.UtcNow,
                WorkItemId = workItemId,
                Source = systemName
            };

            // Add comment to work item
            workItem.Comments.Add(comment);
            workItem.LastModified = DateTime.UtcNow;

            // Update the work item in the source system
            await adapter.Upsert(workItem, CancellationToken.None);

            // If sync is enabled, sync to other systems
            if (syncComment && adapters.Count > 1)
            {
                var otherAdapters = adapters.Where(a => a.SystemName != systemName).ToList();
                foreach (var otherAdapter in otherAdapters)
                {
                    try
                    {
                        // Set ExternalId for cross-system tracking
                        if (string.IsNullOrEmpty(workItem.ExternalId))
                        {
                            workItem.ExternalId = ExternalIdHelper.CreateExternalId(systemName, workItemId);
                        }
                        
                        await otherAdapter.Initialize(CancellationToken.None);
                        await otherAdapter.Upsert(workItem, CancellationToken.None);
                        _logger.LogInformation("Comment synced to {System}", otherAdapter.SystemName);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error syncing comment to {System}", otherAdapter.SystemName);
                    }
                }
            }

            // Update session storage
            var workItemsJson = HttpContext.Session.GetString("WorkItems");
            if (!string.IsNullOrEmpty(workItemsJson))
            {
                var workItems = JsonSerializer.Deserialize<List<WorkItem>>(workItemsJson) ?? new();
                var existingIndex = workItems.FindIndex(w => w.Id == workItemId);
                if (existingIndex >= 0)
                {
                    workItems[existingIndex] = workItem;
                    HttpContext.Session.SetString("WorkItems", JsonSerializer.Serialize(workItems));
                }
            }

            TempData["Success"] = syncComment
                ? "Comment added and synced to other systems!"
                : "Comment added successfully!";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding comment");
            TempData["Error"] = $"Error adding comment: {ex.Message}";
        }

        return RedirectToPage(new { id = workItemId, system = systemName });
    }

    public async Task<IActionResult> OnPostSyncWorkItemAsync(string workItemId, string systemName)
    {
        try
        {
            _logger.LogInformation("Syncing work item {WorkItemId} from {System}",
                workItemId, systemName);

            var adapters = GetConfiguredAdapters();
            var sourceAdapter = adapters.FirstOrDefault(a => a.SystemName == systemName);
            if (sourceAdapter == null)
            {
                TempData["Error"] = $"Source adapter '{systemName}' not found or not configured.";
                return RedirectToPage(new { id = workItemId, system = systemName });
            }

            await sourceAdapter.Initialize(CancellationToken.None);

            // Get the work item
            var entity = await sourceAdapter.GetById(workItemId, CancellationToken.None);
            var workItem = entity as WorkItem;

            if (workItem == null)
            {
                TempData["Error"] = "Work item not found.";
                return RedirectToPage(new { id = workItemId, system = systemName });
            }

            // Set ExternalId for cross-system tracking
            if (string.IsNullOrEmpty(workItem.ExternalId))
            {
                workItem.ExternalId = ExternalIdHelper.CreateExternalId(systemName, workItemId);
            }

            // Sync to all other systems
            var otherAdapters = adapters.Where(a => a.SystemName != systemName).ToList();
            int successCount = 0;
            int failCount = 0;

            foreach (var otherAdapter in otherAdapters)
            {
                try
                {
                    await otherAdapter.Initialize(CancellationToken.None);
                    await otherAdapter.Upsert(workItem, CancellationToken.None);
                    _logger.LogInformation("Work item synced to {System}", otherAdapter.SystemName);
                    successCount++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error syncing to {System}", otherAdapter.SystemName);
                    failCount++;
                }
            }

            if (failCount == 0)
            {
                TempData["Success"] = $"Work item synced to {successCount} system(s) successfully!";
            }
            else
            {
                TempData["Error"] = $"Sync completed with issues: {successCount} succeeded, {failCount} failed.";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error syncing work item");
            TempData["Error"] = $"Error syncing work item: {ex.Message}";
        }

        return RedirectToPage(new { id = workItemId, system = systemName });
    }
}
