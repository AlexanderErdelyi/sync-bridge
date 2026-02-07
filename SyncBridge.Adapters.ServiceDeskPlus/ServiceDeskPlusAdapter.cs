using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http.Json;
using System.Text.Json;
using SyncBridge.Core.Configuration;
using SyncBridge.Core.Interfaces;
using SyncBridge.Core.Models;

namespace SyncBridge.Adapters.ServiceDeskPlus;

/// <summary>
/// Adapter for ManageEngine ServiceDesk Plus
/// </summary>
public class ServiceDeskPlusAdapter : ISyncAdapter
{
    private readonly ILogger<ServiceDeskPlusAdapter> _logger;
    private readonly ServiceDeskPlusConfig _config;
    private readonly HttpClient _httpClient;

    public string SystemName => "ServiceDeskPlus";

    public ServiceDeskPlusAdapter(
        ILogger<ServiceDeskPlusAdapter> logger,
        IOptions<AdapterConfiguration> config,
        HttpClient httpClient)
    {
        _logger = logger;
        _config = config.Value.ServiceDeskPlus 
            ?? throw new ArgumentException("ServiceDesk Plus configuration is required");
        _httpClient = httpClient;
    }

    public Task Initialize(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Initializing ServiceDesk Plus adapter for {BaseUrl}", _config.BaseUrl);

        _httpClient.BaseAddress = new Uri(_config.BaseUrl);
        _httpClient.DefaultRequestHeaders.Add("technician_key", _config.TechnicianKey);

        _logger.LogInformation("ServiceDesk Plus adapter initialized successfully");
        return Task.CompletedTask;
    }

    public async Task<IEnumerable<SyncChange>> GetChanges(DateTime since, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Getting changes from ServiceDesk Plus since {Since}", since);

        var changes = new List<SyncChange>();

        try
        {
            // Build query for modified requests
            var sinceTimestamp = new DateTimeOffset(since).ToUnixTimeMilliseconds();
            var url = $"/api/v3/requests?input_data={{\"list_info\":{{\"row_count\":100,\"start_index\":1,\"search_criteria\":{{\"field\":\"last_updated_time\",\"condition\":\"greater than\",\"value\":\"{sinceTimestamp}\"}}}}}}&format_data=json";

            var response = await _httpClient.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var jsonDoc = JsonDocument.Parse(content);

            if (jsonDoc.RootElement.TryGetProperty("requests", out var requests))
            {
                foreach (var request in requests.EnumerateArray())
                {
                    var workItem = ConvertToWorkItem(request);
                    changes.Add(new SyncChange
                    {
                        Entity = workItem,
                        ChangeType = Core.Models.ChangeType.Updated,
                        Timestamp = workItem.LastModified
                    });
                }
            }

            _logger.LogInformation("Found {Count} changes in ServiceDesk Plus", changes.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting changes from ServiceDesk Plus");
            throw;
        }

        return changes;
    }

    public async Task<SyncEntity?> GetById(string id, CancellationToken cancellationToken = default)
    {
        try
        {
            var url = $"/api/v3/requests/{id}";
            var response = await _httpClient.GetAsync(url, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
                return null;

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var jsonDoc = JsonDocument.Parse(content);

            if (jsonDoc.RootElement.TryGetProperty("request", out var request))
            {
                return ConvertToWorkItem(request);
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting request {Id} from ServiceDesk Plus", id);
            return null;
        }
    }

    public async Task<SyncEntity> Upsert(SyncEntity entity, CancellationToken cancellationToken = default)
    {
        if (entity is not WorkItem workItem)
            throw new ArgumentException("Entity must be a WorkItem", nameof(entity));

        try
        {
            // Check if request already exists
            if (!string.IsNullOrEmpty(workItem.ExternalId) && !string.IsNullOrEmpty(workItem.Id))
            {
                // Update existing request
                _logger.LogDebug("Updating ServiceDesk Plus request {Id}", workItem.Id);
                var updateData = CreateRequestData(workItem);
                
                var url = $"/api/v3/requests/{workItem.Id}";
                var response = await _httpClient.PutAsJsonAsync(url, updateData, cancellationToken);
                response.EnsureSuccessStatusCode();

                // Sync comments
                await SyncComments(workItem.Id, workItem.Comments, cancellationToken);

                return await GetById(workItem.Id, cancellationToken) ?? workItem;
            }
            else
            {
                // Create new request
                _logger.LogDebug("Creating new ServiceDesk Plus request");
                var requestData = CreateRequestData(workItem);
                
                var url = "/api/v3/requests";
                var response = await _httpClient.PostAsJsonAsync(url, requestData, cancellationToken);
                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                var jsonDoc = JsonDocument.Parse(content);

                if (jsonDoc.RootElement.TryGetProperty("request", out var request))
                {
                    var created = ConvertToWorkItem(request);
                    
                    // Sync comments
                    await SyncComments(created.Id, workItem.Comments, cancellationToken);
                    
                    return created;
                }

                return workItem;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error upserting request in ServiceDesk Plus");
            throw;
        }
    }

    private async Task SyncComments(string requestId, List<Comment> comments, CancellationToken cancellationToken)
    {
        if (!comments.Any())
            return;

        foreach (var comment in comments)
        {
            try
            {
                // Only create new comments (no external ID)
                if (string.IsNullOrEmpty(comment.ExternalId))
                {
                    var commentData = new
                    {
                        request_note = new
                        {
                            description = comment.Text,
                            show_to_requester = true
                        }
                    };

                    var url = $"/api/v3/requests/{requestId}/notes";
                    await _httpClient.PostAsJsonAsync(url, commentData, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to sync comment for request {Id}", requestId);
            }
        }
    }

    private object CreateRequestData(WorkItem workItem)
    {
        var data = new Dictionary<string, object>
        {
            ["subject"] = workItem.Title,
            ["description"] = workItem.Description
        };

        if (!string.IsNullOrEmpty(workItem.State))
        {
            data["status"] = new { name = workItem.State };
        }

        if (!string.IsNullOrEmpty(workItem.Priority))
        {
            data["priority"] = new { name = workItem.Priority };
        }

        if (!string.IsNullOrEmpty(workItem.AssignedTo))
        {
            data["technician"] = new { name = workItem.AssignedTo };
        }

        // Add external ID as a custom field
        if (!string.IsNullOrEmpty(workItem.ExternalId))
        {
            data["udf_fields"] = new { udf_char1 = workItem.ExternalId };
        }

        return new { request = data };
    }

    private WorkItem ConvertToWorkItem(JsonElement request)
    {
        var workItem = new WorkItem
        {
            Id = GetStringProperty(request, "id") ?? string.Empty,
            Source = SystemName,
            Title = GetStringProperty(request, "subject") ?? string.Empty,
            Description = GetStringProperty(request, "description") ?? string.Empty,
            Type = "Request"
        };

        // Get status
        if (request.TryGetProperty("status", out var status))
        {
            workItem.State = GetStringProperty(status, "name") ?? string.Empty;
        }

        // Get priority
        if (request.TryGetProperty("priority", out var priority))
        {
            workItem.Priority = GetStringProperty(priority, "name");
        }

        // Get assigned technician
        if (request.TryGetProperty("technician", out var technician))
        {
            workItem.AssignedTo = GetStringProperty(technician, "name");
        }

        // Get last modified time
        if (request.TryGetProperty("last_updated_time", out var lastUpdated))
        {
            if (lastUpdated.TryGetProperty("value", out var value) && value.ValueKind == JsonValueKind.Number)
            {
                var timestamp = value.GetInt64();
                workItem.LastModified = DateTimeOffset.FromUnixTimeMilliseconds(timestamp).DateTime;
            }
        }

        // Get external ID from custom field
        if (request.TryGetProperty("udf_fields", out var udfFields))
        {
            workItem.ExternalId = GetStringProperty(udfFields, "udf_char1");
        }

        return workItem;
    }

    private string? GetStringProperty(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var property))
        {
            if (property.ValueKind == JsonValueKind.String)
                return property.GetString();
            else if (property.ValueKind == JsonValueKind.Number)
                return property.GetInt64().ToString();
        }
        return null;
    }
}
