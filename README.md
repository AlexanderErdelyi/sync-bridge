# Sync Bridge

A lightweight, extensible platform for bidirectional synchronization between systems such as Azure DevOps, CRM platforms, and ServiceDesk Plus. Designed for reliability, modularity, and seamless cross-system data flow.

## Features

- **Bidirectional Synchronization**: Sync work items, tickets, and comments between systems
- **Extensible Architecture**: Easy to add new system adapters
- **Background Service**: Run as a service for automatic synchronization
- **Azure DevOps Support**: Full integration with Azure DevOps work items
- **ServiceDesk Plus Support**: Integration with ManageEngine ServiceDesk Plus
- **Mock CRM**: Testing adapter for development and testing

## Projects

- **SyncBridge.Core**: Core library with interfaces, models, and sync engine
- **SyncBridge.CLI**: Command-line interface and service host
- **SyncBridge.Adapters.AzureDevOps**: Azure DevOps adapter
- **SyncBridge.Adapters.ServiceDeskPlus**: ServiceDesk Plus adapter
- **SyncBridge.Adapters.Crm.Mock**: Mock CRM adapter for testing

## Architecture

### Core Interfaces

The `ISyncAdapter` interface defines the contract for all adapters:

```csharp
public interface ISyncAdapter
{
    string SystemName { get; }
    Task<IEnumerable<SyncChange>> GetChanges(DateTime since, CancellationToken cancellationToken);
    Task<SyncEntity?> GetById(string id, CancellationToken cancellationToken);
    Task<SyncEntity> Upsert(SyncEntity entity, CancellationToken cancellationToken);
    Task Initialize(CancellationToken cancellationToken);
}
```

### Domain Models

- **SyncEntity**: Base class for all synchronizable entities
- **WorkItem**: Represents work items, tickets, tasks, etc.
- **Comment**: Represents comments on work items
- **SyncChange**: Represents a detected change during synchronization

### Sync Engine

The `SyncEngine` performs bidirectional synchronization between two adapters:
- Detects changes since last sync
- Applies changes to target system
- Handles comments and custom fields
- Provides detailed sync results

### Background Service

The `SyncBackgroundService` runs continuously:
- Polls for changes at configured intervals
- Synchronizes all configured mappings
- Provides logging and error handling

## Configuration

Configure the service in `appsettings.json`:

```json
{
  "Sync": {
    "PollIntervalSeconds": 60,
    "BatchSize": 100,
    "SyncComments": true,
    "Mappings": [
      {
        "SourceSystem": "AzureDevOps",
        "TargetSystem": "ServiceDeskPlus",
        "Bidirectional": true
      }
    ]
  },
  "Adapters": {
    "AzureDevOps": {
      "OrganizationUrl": "https://dev.azure.com/your-organization",
      "PersonalAccessToken": "your-pat-token",
      "Project": "YourProject"
    },
    "ServiceDeskPlus": {
      "BaseUrl": "https://your-sdp-instance.com",
      "ApiKey": "your-api-key",
      "TechnicianKey": "your-technician-key"
    }
  }
}
```

## Getting Started

### Prerequisites

- .NET 8 SDK
- Azure DevOps account with Personal Access Token (PAT)
- ServiceDesk Plus instance with API credentials

### Building

```bash
dotnet build
```

### Running

```bash
cd SyncBridge.CLI
dotnet run
```

The service will start and begin synchronizing based on your configuration.

### Running as a Service

The application can be deployed as a Windows Service or Linux systemd service for continuous operation.

#### Windows Service

Use tools like `sc.exe` or NSSM to install the application as a Windows Service.

#### Linux systemd

Create a systemd unit file:

```ini
[Unit]
Description=Sync Bridge Service
After=network.target

[Service]
Type=notify
ExecStart=/usr/bin/dotnet /path/to/SyncBridge.CLI.dll
Restart=always
User=syncbridge
WorkingDirectory=/path/to/

[Install]
WantedBy=multi-user.target
```

## Extending with New Adapters

To add a new system adapter:

1. Create a new class library project
2. Reference `SyncBridge.Core`
3. Implement the `ISyncAdapter` interface
4. Convert between your system's types and `WorkItem`/`Comment` models
5. Register your adapter in the CLI's dependency injection container

Example:

```csharp
public class MySystemAdapter : ISyncAdapter
{
    public string SystemName => "MySystem";
    
    public async Task<IEnumerable<SyncChange>> GetChanges(DateTime since, CancellationToken ct)
    {
        // Query your system for changes
    }
    
    public async Task<SyncEntity?> GetById(string id, CancellationToken ct)
    {
        // Get specific entity by ID
    }
    
    public async Task<SyncEntity> Upsert(SyncEntity entity, CancellationToken ct)
    {
        // Create or update entity in your system
    }
    
    public async Task Initialize(CancellationToken ct)
    {
        // Connect to your system
    }
}
```

## Security Considerations

- Store credentials securely (use environment variables, Azure Key Vault, etc.)
- Use HTTPS for all API communications
- Implement proper authentication and authorization
- Regularly rotate access tokens and API keys
- Review audit logs for suspicious activity

## License

MIT

## Contributing

Contributions are welcome! Please submit pull requests or open issues for bugs and feature requests.
