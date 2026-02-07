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
            // Note: Azure DevOps requires date-only format (yyyy-MM-dd) for ChangedDate queries
            var wiql = new Wiql
            {
                Query = $@"
                    SELECT [System.Id], [System.ChangedDate]
                    FROM WorkItems
                    WHERE [System.TeamProject] = '{_config.Project}'
                    AND [System.ChangedDate] >= '{since:yyyy-MM-dd}'
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
                    var syncEntity = await ConvertToWorkItem(workItem, cancellationToken);
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

            return await ConvertToWorkItem(workItem, cancellationToken);
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
            // First, try to find existing work item by ExternalId
            AzureWorkItem? existingWorkItem = null;
            
            if (!string.IsNullOrEmpty(workItem.ExternalId))
            {
                // Search for work item with matching ExternalId tag
                var wiql = new Wiql
                {
                    Query = $@"
                        SELECT [System.Id]
                        FROM WorkItems
                        WHERE [System.TeamProject] = '{_config.Project}'
                        AND [System.Tags] CONTAINS 'ExternalId:{workItem.ExternalId}'"
                };

                var result = await _workItemClient.QueryByWiqlAsync(wiql, cancellationToken: cancellationToken);
                if (result.WorkItems.Any())
                {
                    var existingId = result.WorkItems.First().Id;
                    existingWorkItem = await _workItemClient.GetWorkItemAsync(existingId, cancellationToken: cancellationToken);
                }
            }

            // Check if work item already exists (has ID from Azure DevOps or found by ExternalId)
            int? idToUpdate = null;
            
            if (existingWorkItem != null)
            {
                idToUpdate = existingWorkItem.Id;
            }
            else if (!string.IsNullOrEmpty(workItem.Id) && int.TryParse(workItem.Id, out var parsedId) && workItem.Source == SystemName)
            {
                idToUpdate = parsedId;
            }
            
            if (idToUpdate.HasValue)
            {
                // Update existing Azure DevOps work item
                _logger.LogDebug("Updating Azure DevOps work item {Id}", idToUpdate.Value);
                
                // Get the current work item to check its state
                var current = existingWorkItem ?? await _workItemClient.GetWorkItemAsync(idToUpdate.Value, cancellationToken: cancellationToken);
                var patchDocument = CreatePatchDocument(workItem, isUpdate: true, currentWorkItem: current);
                
                var updated = await _workItemClient.UpdateWorkItemAsync(
                    patchDocument,
                    idToUpdate.Value,
                    cancellationToken: cancellationToken);

                // Add new comments (comments without ExternalId)
                await SyncComments(updated.Id!.Value, workItem.Comments, cancellationToken);

                return await ConvertToWorkItem(updated, cancellationToken);
            }
            else
            {
                // Create new work item
                _logger.LogDebug("Creating new Azure DevOps work item");
                var patchDocument = CreatePatchDocument(workItem, isUpdate: false, currentWorkItem: null);
                var created = await _workItemClient.CreateWorkItemAsync(
                    patchDocument,
                    _config.Project,
                    workItem.Type,
                    cancellationToken: cancellationToken);

                // Add comments if present
                await SyncComments(created.Id!.Value, workItem.Comments, cancellationToken);

                return await ConvertToWorkItem(created, cancellationToken);
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

        // Get existing comments from Azure DevOps
        var existingComments = await _workItemClient.GetCommentsAsync(_config.Project, workItemId, cancellationToken: cancellationToken);
        var existingTexts = new HashSet<string>(existingComments.Comments.Select(c => c.Text ?? string.Empty));

        foreach (var comment in comments)
        {
            try
            {
                // Only add comment if it doesn't already exist (check by text content)
                if (!existingTexts.Contains(comment.Text))
                {
                    var commentCreate = new CommentCreate 
                    { 
                        Text = $"{comment.Text}\n\n_Added by {comment.Author} on {comment.CreatedDate:yyyy-MM-dd HH:mm}_"
                    };
                    
                    var added = await _workItemClient.AddCommentAsync(
                        commentCreate,
                        _config.Project,
                        workItemId,
                        cancellationToken: cancellationToken);
                    
                    _logger.LogInformation("Comment added to Azure DevOps work item {Id}", workItemId);
                }
                else
                {
                    _logger.LogDebug("Comment already exists in Azure DevOps work item {Id}, skipping", workItemId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to sync comment for work item {Id}", workItemId);
            }
        }
    }

    private JsonPatchDocument CreatePatchDocument(Core.Models.WorkItem workItem, bool isUpdate, AzureWorkItem? currentWorkItem)
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

        // Never update state field when updating existing work items to avoid validation errors
        // Azure DevOps has complex state transition rules that vary by work item type
        if (!isUpdate && !string.IsNullOrEmpty(workItem.State))
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

    private async Task<Core.Models.WorkItem> ConvertToWorkItem(AzureWorkItem azureWorkItem, CancellationToken cancellationToken = default)
    {
        var workItem = new Core.Models.WorkItem
        {
            Id = azureWorkItem.Id?.ToString() ?? string.Empty,
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

        // Load comments from Azure DevOps
        if (_workItemClient != null && azureWorkItem.Id.HasValue)
        {
            try
            {
                var commentsResponse = await _workItemClient.GetCommentsAsync(
                    _config.Project, 
                    azureWorkItem.Id.Value, 
                    cancellationToken: cancellationToken);

                foreach (var azComment in commentsResponse.Comments)
                {
                    workItem.Comments.Add(new Core.Models.Comment
                    {
                        Id = azComment.Id.ToString(),
                        Text = azComment.Text ?? string.Empty,
                        Author = azComment.CreatedBy?.DisplayName ?? "Unknown",
                        CreatedDate = azComment.CreatedDate,
                        WorkItemId = workItem.Id,
                        Source = SystemName,
                        ExternalId = azComment.Id.ToString()
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load comments for work item {Id}", azureWorkItem.Id);
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
