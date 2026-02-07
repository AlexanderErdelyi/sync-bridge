# Development Guide

## Project Structure

```
SyncBridge/
├── SyncBridge.Core/              # Core library
│   ├── Interfaces/               # ISyncAdapter interface
│   ├── Models/                   # Domain models
│   ├── Services/                 # SyncEngine and Background Service
│   └── Configuration/            # Configuration models
├── SyncBridge.CLI/               # Command-line host application
├── SyncBridge.Adapters.AzureDevOps/      # Azure DevOps integration
├── SyncBridge.Adapters.ServiceDeskPlus/  # ServiceDesk Plus integration
└── SyncBridge.Adapters.Crm.Mock/         # Mock adapter for testing
```

## Building and Testing

### Build the solution
```bash
cd /path/to/sync-bridge
dotnet build
```

### Run tests (when implemented)
```bash
dotnet test
```

### Run the CLI
```bash
cd SyncBridge.CLI
dotnet run
```

## Creating a New Adapter

Follow these steps to add support for a new system:

### 1. Create a new project

```bash
dotnet new classlib -n SyncBridge.Adapters.YourSystem -f net8.0
dotnet sln add SyncBridge.Adapters.YourSystem/SyncBridge.Adapters.YourSystem.csproj
```

### 2. Add reference to Core

```bash
cd SyncBridge.Adapters.YourSystem
dotnet add reference ../SyncBridge.Core/SyncBridge.Core.csproj
```

### 3. Implement ISyncAdapter

```csharp
using SyncBridge.Core.Interfaces;
using SyncBridge.Core.Models;

namespace SyncBridge.Adapters.YourSystem;

public class YourSystemAdapter : ISyncAdapter
{
    public string SystemName => "YourSystem";

    public async Task Initialize(CancellationToken cancellationToken = default)
    {
        // Connect to your system's API
        // Authenticate
        // Store connection objects
    }

    public async Task<IEnumerable<SyncChange>> GetChanges(DateTime since, CancellationToken cancellationToken = default)
    {
        // Query your system for items modified since the given date
        // Convert to SyncChange objects
        // Return the list
    }

    public async Task<SyncEntity?> GetById(string id, CancellationToken cancellationToken = default)
    {
        // Get a specific item by ID
        // Convert to WorkItem or other SyncEntity
        // Return null if not found
    }

    public async Task<SyncEntity> Upsert(SyncEntity entity, CancellationToken cancellationToken = default)
    {
        // Create or update the entity in your system
        // Return the updated entity with new ID and metadata
    }
}
```

### 4. Register in CLI

Edit `SyncBridge.CLI/Program.cs`:

```csharp
// Add reference
builder.Services.AddSingleton<ISyncAdapter, YourSystemAdapter>();
```

### 5. Add configuration

Edit `SyncBridge.Core/Configuration/AdapterConfiguration.cs`:

```csharp
public class YourSystemConfig
{
    public string BaseUrl { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
}
```

Add property to `AdapterConfiguration`:

```csharp
public YourSystemConfig? YourSystem { get; set; }
```

### 6. Update appsettings.json

```json
{
  "Adapters": {
    "YourSystem": {
      "BaseUrl": "https://your-system.com",
      "ApiKey": "your-api-key"
    }
  }
}
```

## Key Concepts

### WorkItem Model

The `WorkItem` model is the primary entity for synchronization:
- `Title`: Display name
- `Description`: Detailed description
- `State`: Current status (New, Active, Resolved, etc.)
- `Priority`: Priority level
- `AssignedTo`: User assigned to the item
- `Type`: Item type (Bug, Task, User Story, etc.)
- `Comments`: List of comments
- `CustomFields`: Dictionary for system-specific fields

### Comment Model

Comments are synchronized along with work items:
- `Text`: Comment content
- `Author`: Who wrote it
- `CreatedDate`: When it was created
- `WorkItemId`: Parent work item

### External ID Tracking

Each entity has an `ExternalId` field used to track cross-system references:
- When syncing from System A to System B, store System A's ID in the `ExternalId` field
- When updating, check `ExternalId` to find the corresponding item
- This enables bidirectional sync without data duplication

## Best Practices

1. **Error Handling**: Always wrap API calls in try-catch blocks
2. **Logging**: Use ILogger extensively for debugging and monitoring
3. **Cancellation**: Respect CancellationToken for graceful shutdown
4. **Authentication**: Store credentials securely (environment variables, Key Vault)
5. **Rate Limiting**: Implement retry logic and respect API rate limits
6. **Testing**: Use the Mock adapter to test sync logic without real systems

## Debugging

Enable detailed logging in `appsettings.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "Microsoft": "Information"
    }
  }
}
```

Run with verbose output:
```bash
dotnet run --verbosity detailed
```

## Common Issues

### Connection Failures
- Verify credentials in appsettings.json
- Check network connectivity
- Ensure API endpoints are accessible
- Validate SSL certificates

### Sync Errors
- Check that field mappings are correct
- Verify required fields are populated
- Review adapter-specific error logs
- Test with Mock adapter first

### Performance Issues
- Reduce `BatchSize` in configuration
- Increase `PollIntervalSeconds`
- Add pagination to GetChanges method
- Consider caching frequently accessed data
