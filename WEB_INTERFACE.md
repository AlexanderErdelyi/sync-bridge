# Sync Bridge Web Interface

## Overview

The Sync Bridge Web Interface is a modern ASP.NET Core Razor Pages application that provides a user-friendly interface for configuring, testing, and managing bidirectional synchronization between Azure DevOps, ServiceDesk Plus, and other systems.

## Features

### 1. Dashboard
- Overview of sync status with last sync results
- Display of active adapters and sync mappings
- Manual sync trigger button
- Quick stats showing adapter count and mapping count
- Real-time sync status with success/failure indicators

### 2. Configuration Management
- **Azure DevOps Configuration**
  - Organization URL
  - Personal Access Token (PAT)
  - Project name
  
- **ServiceDesk Plus Configuration**
  - Base URL
  - API Key
  - Technician Key
  
- **Sync Configuration**
  - Poll interval (in seconds)
  - Batch size
  - Comment synchronization toggle

### 3. Testing & Diagnostics
- **Test Connections**: Verify connectivity to all configured adapters
- **Retrieve Data**: Fetch work items from specific adapters
- **Test Sync**: Perform a test synchronization between systems
- Display connection test results with success/failure status
- Show retrieved work items in a table with details

### 4. Work Items Management
- **Create Work Items**
  - Select target system (AzureDevOps, ServiceDeskPlus, MockCRM)
  - Enter title, description, type, priority, state, and assignee
  - Option to automatically sync to other systems
  - Modal dialog with comprehensive form

- **View Work Items**
  - List all work items with details
  - Filter and search capabilities
  - View work item details including comments

- **Work Item Details**
  - View complete work item information
  - See all comments with author and timestamp
  - Add new comments
  - Sync comments to other systems
  - Manual sync trigger for specific work item

### 5. Comment Synchronization
- Add comments to work items through the web interface
- Optionally sync comments to all configured systems
- View comment history with source system indicators
- Bidirectional comment sync support

## Technology Stack

- **ASP.NET Core 8.0**: Modern web framework
- **Razor Pages**: Server-side page rendering
- **Bootstrap 5**: Responsive UI framework
- **Bootstrap Icons**: Icon library for UI elements
- **jQuery**: Client-side JavaScript library
- **Session State**: Temporary data storage for work items and sync results

## Architecture

### Page Structure

```
SyncBridge.Web/
├── Pages/
│   ├── Index.cshtml/.cs           # Dashboard
│   ├── Configuration.cshtml/.cs   # Configuration management
│   ├── Testing.cshtml/.cs         # Testing & diagnostics
│   ├── WorkItems.cshtml/.cs       # Work items list
│   ├── WorkItemDetails.cshtml/.cs # Work item details
│   └── Shared/
│       └── _Layout.cshtml         # Main layout with navigation
├── Program.cs                      # Application configuration
└── appsettings.json               # Application settings
```

### Dependency Injection

The web application uses ASP.NET Core's built-in DI container to manage services:

- **SyncEngine**: Core synchronization engine
- **ISyncAdapter**: Registered adapters (AzureDevOps, ServiceDeskPlus, MockCRM)
- **IOptions<SyncConfiguration>**: Sync configuration
- **IOptions<AdapterConfiguration>**: Adapter configuration
- **Session**: For temporary data storage

### Data Flow

1. **Configuration**: User enters configuration through web forms → stored in session → used by adapters
2. **Testing**: User triggers tests → adapters initialize → connection/retrieval performed → results displayed
3. **Work Items**: User creates work item → saved to target adapter → optionally synced to other adapters → stored in session
4. **Comments**: User adds comment → added to work item → work item updated in adapter → optionally synced → session updated
5. **Sync**: User triggers sync → SyncEngine performs bidirectional sync → results displayed on dashboard

## User Interface Design

### Color Scheme
- **Primary (Blue)**: Configuration and main actions
- **Success (Green)**: Testing and successful operations
- **Info (Cyan)**: Work items and informational content
- **Warning (Yellow)**: Sync actions and warnings
- **Danger (Red)**: Errors and failures
- **Dark**: Headers and contrast elements

### Navigation
- Fixed top navigation bar with app branding
- Four main menu items: Dashboard, Configuration, Testing, Work Items
- Responsive design for mobile and desktop

### Alerts & Notifications
- TempData-based success/error messages
- Bootstrap alert components with auto-dismiss
- Color-coded status indicators

## Session Management

The application uses ASP.NET Core session state to store:
- Last sync result (for dashboard display)
- Work items list (for quick access without re-fetching)
- Configuration overrides (when saved through UI)

Session timeout: 30 minutes

## Security Considerations

### Current Implementation
- Configuration stored in session (temporary)
- Passwords displayed as password fields
- No authentication/authorization (single-user assumption)

### Production Recommendations
1. Implement user authentication (ASP.NET Core Identity)
2. Add role-based authorization
3. Store configuration in encrypted database or Azure Key Vault
4. Use HTTPS in production
5. Implement CSRF protection (already included via anti-forgery tokens)
6. Add input validation and sanitization
7. Implement rate limiting
8. Add audit logging

## Running the Web Interface

### Development
```bash
cd SyncBridge.Web
dotnet run
```

Access at: http://localhost:5000 or https://localhost:5001

### Production
```bash
dotnet publish -c Release -o ./publish
cd publish
dotnet SyncBridge.Web.dll
```

### Docker (Future)
```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:8.0
COPY ./publish /app
WORKDIR /app
EXPOSE 80
ENTRYPOINT ["dotnet", "SyncBridge.Web.dll"]
```

## Configuration

Configuration is managed through `appsettings.json`:

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
      "PersonalAccessToken": "your-pat-token-here",
      "Project": "YourProject"
    },
    "ServiceDeskPlus": {
      "BaseUrl": "https://your-sdp-instance.com",
      "ApiKey": "your-api-key-here",
      "TechnicianKey": "your-technician-key-here"
    },
    "MockCrm": {
      "Enabled": true
    }
  }
}
```

## Future Enhancements

### Planned Features
1. **Real-time Sync Monitoring**: WebSocket-based live sync updates
2. **Sync History**: Persistent storage of sync results
3. **User Management**: Multi-user support with authentication
4. **Dashboard Widgets**: Customizable dashboard with charts and graphs
5. **Notification System**: Email/webhook notifications for sync events
6. **Advanced Filtering**: Filter work items by date, status, system, etc.
7. **Bulk Operations**: Bulk create, update, or sync work items
8. **Conflict Resolution UI**: Manual conflict resolution interface
9. **Scheduler UI**: Configure sync schedules through the UI
10. **API Endpoint**: RESTful API for programmatic access

### Technical Improvements
1. Move from session-based storage to database (Entity Framework Core)
2. Add SignalR for real-time updates
3. Implement background services for automatic sync within web app
4. Add comprehensive logging and monitoring
5. Implement caching for frequently accessed data
6. Add health checks endpoint
7. Implement proper configuration management (no session storage)
8. Add comprehensive unit and integration tests

## Troubleshooting

### Common Issues

**Issue**: Web app won't start
- **Solution**: Check that port 5000/5001 is not in use, verify .NET 8 SDK is installed

**Issue**: Configuration not saving
- **Solution**: Configuration is currently session-based (temporary). Restart will lose settings. Use appsettings.json for persistent configuration.

**Issue**: Sync not working through UI
- **Solution**: Verify adapters are properly configured in appsettings.json, check console logs for errors

**Issue**: Work items not appearing
- **Solution**: Click "Retrieve Work Items" on Testing page first, or create a new work item

## Contributing

When contributing to the web interface:

1. Follow ASP.NET Core Razor Pages conventions
2. Use Bootstrap components for consistency
3. Add appropriate error handling and logging
4. Update this documentation for new features
5. Test all pages and functionality before submitting PR
6. Ensure responsive design works on mobile devices

## License

MIT License - see main LICENSE file in repository root
