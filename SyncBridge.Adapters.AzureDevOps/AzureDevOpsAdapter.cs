using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;
using Microsoft.VisualStudio.Services.WebApi.Patch;
using Microsoft.VisualStudio.Services.WebApi.Patch.Json;
using SyncBridge.Core.Configuration;
using SyncBridge.Core.Interfaces;
using SyncBridge.Core.Models;
using AzureComment = Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models.Comment;
using AzureWorkItem = Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models.WorkItem;

namespace SyncBridge.Adapters.AzureDevOps;

/// <summary>
/// Adapter for Azure DevOps work items
/// </summary>
public class AzureDevOpsAdapter : ISyncAdapter
{
    private readonly ILogger<AzureDevOpsAdapter> _logger;
    private readonly AzureDevOpsConfig _config;
    private VssConnection? _connection;
    private WorkItemTrackingHttpClient? _workItemClient;

    public string SystemName => "AzureDevOps";

    public AzureDevOpsAdapter(
        ILogger<AzureDevOpsAdapter> logger,
        IOptions<AdapterConfiguration> config)
    {
        _logger = logger;
        _config = config.Value.AzureDevOps 
            ?? throw new ArgumentException("Azure DevOps configuration is required");
    }

    public async Task Initialize(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Initializing Azure DevOps adapter for {Org}/{Project}",
            _config.OrganizationUrl, _config.Project);

        var credentials = new VssBasicCredential(string.Empty, _config.PersonalAccessToken);
        _connection = new VssConnection(new Uri(_config.OrganizationUrl), credentials);
        
        await _connection.ConnectAsync(cancellationToken);
        _workItemClient = await _connection.GetClientAsync<WorkItemTrackingHttpClient>(cancellationToken);

        _logger.LogInformation("Azure DevOps adapter initialized successfully");
    }

    public async Task<IEnumerable<SyncChange>> GetChanges(DateTime since, CancellationToken cancellationToken = default)
    {
        if (_workItemClient == null)
            throw new InvalidOperationException("Adapter not initialized");

        _logger.LogDebug("Getting changes from Azure DevOps since {Since}", since);

        var changes = new List<SyncChange>();

        try
        {
            // Query for work items updated since the specified date
            var wiql = new Wiql
            {
                Query = $@"
                    SELECT [System.Id], [System.ChangedDate]
                    FROM WorkItems
                    WHERE [System.TeamProject] = '{_config.Project}'
                    AND [System.ChangedDate] >= '{since:yyyy-MM-ddTHH:mm:ssZ}'
                    ORDER BY [System.ChangedDate] DESC"
            };

            var result = await _workItemClient.QueryByWiqlAsync(wiql, cancellationToken: cancellationToken);

            if (result.WorkItems.Any())
            {
                var ids = result.WorkItems.Select(wi => wi.Id).ToArray();
                var workItems = await _workItemClient.GetWorkItemsAsync(
                    ids,
                    expand: WorkItemExpand.All,
                    cancellationToken: cancellationToken);

                foreach (var workItem in workItems)
                {
                    var syncEntity = ConvertToWorkItem(workItem);
                    changes.Add(new SyncChange
                    {
                        Entity = syncEntity,
                        ChangeType = Core.Models.ChangeType.Updated,
                        Timestamp = workItem.Fields.ContainsKey("System.ChangedDate")
                            ? (DateTime)workItem.Fields["System.ChangedDate"]
                            : DateTime.UtcNow
                    });
                }
            }

            _logger.LogInformation("Found {Count} changes in Azure DevOps", changes.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting changes from Azure DevOps");
            throw;
        }

        return changes;
    }

    public async Task<SyncEntity?> GetById(string id, CancellationToken cancellationToken = default)
    {
        if (_workItemClient == null)
            throw new InvalidOperationException("Adapter not initialized");

        try
        {
            var workItem = await _workItemClient.GetWorkItemAsync(
                int.Parse(id),
                expand: WorkItemExpand.All,
                cancellationToken: cancellationToken);

            return ConvertToWorkItem(workItem);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting work item {Id} from Azure DevOps", id);
            return null;
        }
    }

    public async Task<SyncEntity> Upsert(SyncEntity entity, CancellationToken cancellationToken = default)
    {
        if (_workItemClient == null)
            throw new InvalidOperationException("Adapter not initialized");

        if (entity is not Core.Models.WorkItem workItem)
            throw new ArgumentException("Entity must be a WorkItem", nameof(entity));

        try
        {
            // Check if work item already exists (has external ID)
            if (!string.IsNullOrEmpty(workItem.ExternalId) && int.TryParse(workItem.Id, out var existingId))
            {
                // Update existing work item
                _logger.LogDebug("Updating Azure DevOps work item {Id}", workItem.Id);
                var patchDocument = CreatePatchDocument(workItem, isUpdate: true);
                var updated = await _workItemClient.UpdateWorkItemAsync(
                    patchDocument,
                    existingId,
                    cancellationToken: cancellationToken);

                // Add comments if present
                await SyncComments(updated.Id!.Value, workItem.Comments, cancellationToken);

                return ConvertToWorkItem(updated);
            }
            else
            {
                // Create new work item
                _logger.LogDebug("Creating new Azure DevOps work item");
                var patchDocument = CreatePatchDocument(workItem, isUpdate: false);
                var created = await _workItemClient.CreateWorkItemAsync(
                    patchDocument,
                    _config.Project,
                    workItem.Type,
                    cancellationToken: cancellationToken);

                // Add comments if present
                await SyncComments(created.Id!.Value, workItem.Comments, cancellationToken);

                return ConvertToWorkItem(created);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error upserting work item in Azure DevOps");
            throw;
        }
    }

    private async Task SyncComments(int workItemId, List<Core.Models.Comment> comments, CancellationToken cancellationToken)
    {
        if (_workItemClient == null || !comments.Any())
            return;

        foreach (var comment in comments)
        {
            try
            {
                // Check if comment already exists by external ID
                if (string.IsNullOrEmpty(comment.ExternalId))
                {
                    await _workItemClient.AddCommentAsync(
                        new CommentCreate { Text = comment.Text },
                        _config.Project,
                        workItemId,
                        cancellationToken: cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to sync comment for work item {Id}", workItemId);
            }
        }
    }

    private JsonPatchDocument CreatePatchDocument(Core.Models.WorkItem workItem, bool isUpdate)
    {
        var patchDocument = new JsonPatchDocument();

        patchDocument.Add(new JsonPatchOperation
        {
            Operation = Operation.Add,
            Path = "/fields/System.Title",
            Value = workItem.Title
        });

        patchDocument.Add(new JsonPatchOperation
        {
            Operation = Operation.Add,
            Path = "/fields/System.Description",
            Value = workItem.Description
        });

        if (!string.IsNullOrEmpty(workItem.State))
        {
            patchDocument.Add(new JsonPatchOperation
            {
                Operation = Operation.Add,
                Path = "/fields/System.State",
                Value = workItem.State
            });
        }

        if (!string.IsNullOrEmpty(workItem.Priority))
        {
            patchDocument.Add(new JsonPatchOperation
            {
                Operation = Operation.Add,
                Path = "/fields/Microsoft.VSTS.Common.Priority",
                Value = workItem.Priority
            });
        }

        if (!string.IsNullOrEmpty(workItem.AssignedTo))
        {
            patchDocument.Add(new JsonPatchOperation
            {
                Operation = Operation.Add,
                Path = "/fields/System.AssignedTo",
                Value = workItem.AssignedTo
            });
        }

        // Add external ID as a tag for tracking
        if (!string.IsNullOrEmpty(workItem.ExternalId))
        {
            patchDocument.Add(new JsonPatchOperation
            {
                Operation = Operation.Add,
                Path = "/fields/System.Tags",
                Value = $"ExternalId:{workItem.ExternalId}"
            });
        }

        return patchDocument;
    }

    private Core.Models.WorkItem ConvertToWorkItem(AzureWorkItem azureWorkItem)
    {
        var workItem = new Core.Models.WorkItem
        {
            Id = azureWorkItem.Id.ToString()!,
            Source = SystemName,
            Title = GetFieldValue<string>(azureWorkItem, "System.Title") ?? string.Empty,
            Description = GetFieldValue<string>(azureWorkItem, "System.Description") ?? string.Empty,
            State = GetFieldValue<string>(azureWorkItem, "System.State") ?? string.Empty,
            Priority = GetFieldValue<string>(azureWorkItem, "Microsoft.VSTS.Common.Priority"),
            AssignedTo = GetFieldValue<string>(azureWorkItem, "System.AssignedTo"),
            Type = GetFieldValue<string>(azureWorkItem, "System.WorkItemType") ?? "Task",
            LastModified = GetFieldValue<DateTime>(azureWorkItem, "System.ChangedDate")
        };

        // Extract external ID from tags if present
        var tags = GetFieldValue<string>(azureWorkItem, "System.Tags");
        if (!string.IsNullOrEmpty(tags))
        {
            var externalIdTag = tags.Split(';')
                .FirstOrDefault(t => t.Trim().StartsWith("ExternalId:"));
            if (externalIdTag != null)
            {
                workItem.ExternalId = externalIdTag.Replace("ExternalId:", "").Trim();
            }
        }

        return workItem;
    }

    private T? GetFieldValue<T>(AzureWorkItem workItem, string fieldName)
    {
        if (workItem.Fields != null && workItem.Fields.TryGetValue(fieldName, out var value))
        {
            if (value is T typedValue)
                return typedValue;
            
            try
            {
                return (T)Convert.ChangeType(value, typeof(T));
            }
            catch
            {
                return default;
            }
        }
        return default;
    }
}
