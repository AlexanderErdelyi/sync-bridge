# SyncBridge CLI Configuration

## Configuration Files

- `appsettings.json` - Default configuration (template with placeholder values)
- `appsettings.example.json` - Example configuration file
- `appsettings.Development.json` - Development-specific settings (git-ignored)
- `appsettings.Production.json` - Production-specific settings (git-ignored)

## ⚠️ Security Warning

**NEVER commit actual credentials to version control!**

The `.gitignore` file excludes `appsettings.Development.json` and `appsettings.Production.json` to prevent accidental credential exposure.

## Setup Instructions

1. Copy `appsettings.example.json` to `appsettings.Development.json`
2. Update the configuration with your actual credentials:
   - Azure DevOps Personal Access Token
   - ServiceDesk Plus API Key and Technician Key
3. Run the application: `dotnet run`

## Configuration Options

### Sync Settings

- `PollIntervalSeconds`: How often to check for changes (default: 60)
- `BatchSize`: Maximum items to sync per batch (default: 100)
- `SyncComments`: Whether to sync comments (default: true)

### Azure DevOps

- `OrganizationUrl`: Your Azure DevOps organization URL (e.g., https://dev.azure.com/myorg)
- `PersonalAccessToken`: PAT with Work Items read/write permissions
- `Project`: Project name to sync

### ServiceDesk Plus

- `BaseUrl`: Your ServiceDesk Plus instance URL
- `ApiKey`: API key for authentication
- `TechnicianKey`: Technician key for API access

### Mock CRM

- `Enabled`: Set to true to enable the mock adapter (for testing)

## Environment Variables

You can also configure using environment variables:

```bash
export Adapters__AzureDevOps__PersonalAccessToken="your-token"
export Adapters__ServiceDeskPlus__ApiKey="your-key"
```

## Production Deployment

For production:
1. Create `appsettings.Production.json` with production credentials
2. Set environment to Production: `ASPNETCORE_ENVIRONMENT=Production`
3. Consider using Azure Key Vault or similar for credential management
