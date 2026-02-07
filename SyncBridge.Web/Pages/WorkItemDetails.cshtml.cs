using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SyncBridge.Core.Interfaces;
using SyncBridge.Core.Models;
using System.Text.Json;

namespace SyncBridge.Web.Pages;

public class WorkItemDetailsModel : PageModel
{
    private readonly ILogger<WorkItemDetailsModel> _logger;
    private readonly IEnumerable<ISyncAdapter> _adapters;

    public WorkItemDetailsModel(
        ILogger<WorkItemDetailsModel> logger,
        IEnumerable<ISyncAdapter> adapters)
    {
        _logger = logger;
        _adapters = adapters;
    }

    public WorkItem? WorkItem { get; set; }

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
                var adapter = _adapters.FirstOrDefault(a => a.SystemName == system);
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

            var adapter = _adapters.FirstOrDefault(a => a.SystemName == systemName);
            if (adapter == null)
            {
                TempData["Error"] = $"Adapter '{systemName}' not found.";
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
            if (syncComment && _adapters.Count() > 1)
            {
                var otherAdapters = _adapters.Where(a => a.SystemName != systemName).ToList();
                foreach (var otherAdapter in otherAdapters)
                {
                    try
                    {
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

            var sourceAdapter = _adapters.FirstOrDefault(a => a.SystemName == systemName);
            if (sourceAdapter == null)
            {
                TempData["Error"] = $"Source adapter '{systemName}' not found.";
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

            // Sync to all other systems
            var otherAdapters = _adapters.Where(a => a.SystemName != systemName).ToList();
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
