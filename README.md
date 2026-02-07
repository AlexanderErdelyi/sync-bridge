# Sync Bridge

A lightweight, extensible platform for bidirectional synchronization between systems such as Azure DevOps, CRM platforms, and ServiceDesk Plus. Designed for reliability, modularity, and seamless cross-system data flow.

## Features

- **Bidirectional Synchronization**: Sync work items, tickets, and comments between systems
- **Web User Interface**: Modern web-based UI for configuration, testing, and management
- **Extensible Architecture**: Easy to add new system adapters
- **Background Service**: Run as a service for automatic synchronization
- **Azure DevOps Support**: Full integration with Azure DevOps work items
- **ServiceDesk Plus Support**: Integration with ManageEngine ServiceDesk Plus
- **Mock CRM**: Testing adapter for development and testing
- **Interactive Testing**: Test connections and retrieve data directly from the UI
- **Work Item Management**: Create and sync work items with comments through the web interface

## Projects

- **SyncBridge.Core**: Core library with interfaces, models, and sync engine
- **SyncBridge.Web**: Web application with user interface for configuration and management
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

### Running the Web Interface

The easiest way to get started is with the web interface:

```bash
cd SyncBridge.Web
dotnet run
```

Then open your browser and navigate to `https://localhost:5001` (or `http://localhost:5000`).

#### Web Interface Features

The web interface provides:

1. **Dashboard**: Overview of sync status, active adapters, and quick stats
   - View last sync results
   - Trigger manual synchronization
   - Monitor configured adapters

2. **Configuration**: Manage adapter settings
   - Configure Azure DevOps connection (Organization URL, PAT, Project)
   - Configure ServiceDesk Plus connection (Base URL, API Key, Technician Key)
   - Adjust sync settings (poll interval, batch size, comment sync)

3. **Testing & Diagnostics**: Test your configuration
   - Test connections to all configured adapters
   - Retrieve work items from specific adapters
   - Perform test synchronization

4. **Work Items Management**: Create and manage work items
   - Create new work items in any configured system
   - Automatically sync work items to other systems
   - Add comments to work items
   - Sync comments bidirectionally
   - View work item details and history

### Running the CLI/Background Service

For running as a background service without the web interface:

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

## Screenshots

### Dashboard
![Dashboard](https://github.com/user-attachments/assets/cf1bc3e3-540d-480c-9437-daa9c1d000d8)

The main dashboard provides an overview of your sync status, active adapters, and quick access to all features.

### Configuration
![Configuration](https://github.com/user-attachments/assets/33650212-7c60-4147-b749-979c4ea10a20)

Easily configure your Azure DevOps and ServiceDesk Plus connections, along with sync settings.

### Testing & Diagnostics
![Testing](https://github.com/user-attachments/assets/71073423-4e06-4adc-9b1b-a7cbe11049b2)

Test connections, retrieve data, and perform test synchronizations to verify your configuration.

### Work Items Management
![Work Items](https://github.com/user-attachments/assets/91b150c1-6432-4f19-9af5-4402c35b0707)

Create and manage work items with full bidirectional synchronization support.

### Create Work Item
![Create Work Item](https://github.com/user-attachments/assets/2a8b1bcf-213f-4532-8ae5-fa40816a006a)

Create work items in any configured system and automatically sync them to other systems.

## Usage Examples

### Example 1: Create a Work Item and Sync to DevOps

1. Navigate to the **Work Items** page
2. Click **Create Work Item**
3. Fill in the work item details:
   - Select target system (e.g., Azure DevOps)
   - Enter title and description
   - Set type, priority, state, and assignee
4. Check **Automatically sync to other systems**
5. Click **Create & Sync**

The work item will be created in Azure DevOps and automatically synced to ServiceDesk Plus and other configured systems.

### Example 2: Add a Comment and Sync

1. Navigate to the **Work Items** page
2. Click **View** on any work item
3. Scroll to the **Comments** section
4. Enter your name and comment text
5. Check **Sync comment to other systems**
6. Click **Add Comment & Sync**

The comment will be added to the work item and synced to all configured systems, appearing in Azure DevOps, ServiceDesk Plus, etc.

### Example 3: Test Your Configuration

1. Navigate to the **Testing** page
2. Click **Test All Connections** to verify all adapters can connect
3. Select an adapter and click **Retrieve Work Items** to fetch recent items
4. Click **Test Sync** to perform a test synchronization between systems

### Example 4: Configure Adapters

1. Navigate to the **Configuration** page
2. Enter your Azure DevOps settings:
   - Organization URL: `https://dev.azure.com/your-organization`
   - Personal Access Token (PAT)
   - Project name
3. Enter your ServiceDesk Plus settings:
   - Base URL
   - API Key
   - Technician Key
4. Adjust sync settings:
   - Poll interval (how often to check for changes)
   - Batch size (max items per sync)
   - Enable/disable comment synchronization
5. Click **Save** on each section

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
