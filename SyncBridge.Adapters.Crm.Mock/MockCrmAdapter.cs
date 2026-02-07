using Microsoft.Extensions.Logging;
using SyncBridge.Core.Interfaces;
using SyncBridge.Core.Models;

namespace SyncBridge.Adapters.Crm.Mock;

/// <summary>
/// Mock CRM adapter for testing purposes
/// </summary>
public class MockCrmAdapter : ISyncAdapter
{
    private readonly ILogger<MockCrmAdapter> _logger;
    private readonly Dictionary<string, WorkItem> _mockData = new();
    private int _nextId = 1;

    public string SystemName => "MockCRM";

    public MockCrmAdapter(ILogger<MockCrmAdapter> logger)
    {
        _logger = logger;
    }

    public Task Initialize(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Initializing Mock CRM adapter");

        // Seed with some mock data
        SeedMockData();

        _logger.LogInformation("Mock CRM adapter initialized with {Count} items", _mockData.Count);
        return Task.CompletedTask;
    }

    public Task<IEnumerable<SyncChange>> GetChanges(DateTime since, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Getting changes from Mock CRM since {Since}", since);

        var changes = _mockData.Values
            .Where(wi => wi.LastModified >= since)
            .Select(wi => new SyncChange
            {
                Entity = wi,
                ChangeType = Core.Models.ChangeType.Updated,
                Timestamp = wi.LastModified
            })
            .ToList();

        _logger.LogInformation("Found {Count} changes in Mock CRM", changes.Count);
        return Task.FromResult<IEnumerable<SyncChange>>(changes);
    }

    public Task<SyncEntity?> GetById(string id, CancellationToken cancellationToken = default)
    {
        _mockData.TryGetValue(id, out var workItem);
        return Task.FromResult<SyncEntity?>(workItem);
    }

    public Task<SyncEntity> Upsert(SyncEntity entity, CancellationToken cancellationToken = default)
    {
        if (entity is not WorkItem workItem)
            throw new ArgumentException("Entity must be a WorkItem", nameof(entity));

        // If no ID, create new
        if (string.IsNullOrEmpty(workItem.Id) || !_mockData.ContainsKey(workItem.Id))
        {
            workItem.Id = _nextId++.ToString();
            workItem.Source = SystemName;
            workItem.LastModified = DateTime.UtcNow;
            _mockData[workItem.Id] = workItem;
            _logger.LogDebug("Created new Mock CRM item {Id}", workItem.Id);
        }
        else
        {
            // Update existing
            workItem.LastModified = DateTime.UtcNow;
            _mockData[workItem.Id] = workItem;
            _logger.LogDebug("Updated Mock CRM item {Id}", workItem.Id);
        }

        return Task.FromResult<SyncEntity>(workItem);
    }

    private void SeedMockData()
    {
        var items = new[]
        {
            new WorkItem
            {
                Id = _nextId++.ToString(),
                Source = SystemName,
                Title = "Sample CRM Lead 1",
                Description = "This is a sample lead from CRM",
                State = "Open",
                Priority = "High",
                Type = "Lead",
                LastModified = DateTime.UtcNow.AddDays(-2)
            },
            new WorkItem
            {
                Id = _nextId++.ToString(),
                Source = SystemName,
                Title = "Sample CRM Opportunity 1",
                Description = "This is a sample opportunity from CRM",
                State = "Qualified",
                Priority = "Medium",
                Type = "Opportunity",
                LastModified = DateTime.UtcNow.AddDays(-1)
            },
            new WorkItem
            {
                Id = _nextId++.ToString(),
                Source = SystemName,
                Title = "Sample CRM Case 1",
                Description = "This is a sample case from CRM",
                State = "Active",
                Priority = "Low",
                AssignedTo = "John Doe",
                Type = "Case",
                LastModified = DateTime.UtcNow.AddHours(-12),
                Comments = new List<Comment>
                {
                    new Comment
                    {
                        Id = "1",
                        Text = "Initial investigation started",
                        Author = "Jane Smith",
                        CreatedDate = DateTime.UtcNow.AddHours(-11),
                        LastModified = DateTime.UtcNow.AddHours(-11),
                        Source = SystemName
                    }
                }
            }
        };

        foreach (var item in items)
        {
            _mockData[item.Id] = item;
        }
    }
}
