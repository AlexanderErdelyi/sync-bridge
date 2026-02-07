# Quick Start Guide

Get the Sync Bridge up and running in 5 minutes!

## Prerequisites

- .NET 8 SDK installed ([Download](https://dotnet.microsoft.com/download/dotnet/8.0))
- Azure DevOps account with Personal Access Token
- ServiceDesk Plus instance with API credentials

## Step 1: Clone and Build

```bash
git clone https://github.com/AlexanderErdelyi/sync-bridge.git
cd sync-bridge
dotnet build
```

## Step 2: Configure

```bash
cd SyncBridge.CLI
cp appsettings.example.json appsettings.Development.json
```

Edit `appsettings.Development.json` and add your credentials:

```json
{
  "Adapters": {
    "AzureDevOps": {
      "OrganizationUrl": "https://dev.azure.com/YOUR-ORG",
      "PersonalAccessToken": "YOUR-PAT-TOKEN",
      "Project": "YOUR-PROJECT"
    },
    "ServiceDeskPlus": {
      "BaseUrl": "https://your-instance.servicedeskplus.com",
      "ApiKey": "YOUR-API-KEY",
      "TechnicianKey": "YOUR-TECH-KEY"
    }
  }
}
```

### Getting Azure DevOps PAT

1. Go to https://dev.azure.com/YOUR-ORG
2. Click User Settings (top right) → Personal Access Tokens
3. Create new token with "Work Items (Read & Write)" scope
4. Copy the token (you won't see it again!)

### Getting ServiceDesk Plus API Keys

1. Log into ServiceDesk Plus as admin
2. Go to Admin → API → Generate API Key
3. Copy both API Key and Technician Key

## Step 3: Run

```bash
dotnet run
```

You should see:

```
╔════════════════════════════════════════════╗
║        Sync Bridge Service v1.0           ║
║  Bidirectional Sync Platform for          ║
║  Azure DevOps & ServiceDesk Plus          ║
╚════════════════════════════════════════════╝

info: Sync Background Service starting...
info: Initialized adapter: AzureDevOps
info: Initialized adapter: ServiceDeskPlus
info: Starting sync cycle...
```

## Step 4: Monitor

Watch the console for sync results:

```
info: Sync completed: AzureDevOps <-> ServiceDeskPlus, 5 items synced in 00:00:03
```

Press `Ctrl+C` to stop the service.

## Configuration Tips

### Adjust Sync Frequency

In `appsettings.Development.json`:

```json
{
  "Sync": {
    "PollIntervalSeconds": 300  // Sync every 5 minutes
  }
}
```

### Test with Mock Data

Enable the Mock CRM adapter to test without real systems:

```json
{
  "Sync": {
    "Mappings": [
      {
        "SourceSystem": "MockCRM",
        "TargetSystem": "AzureDevOps",
        "Bidirectional": true
      }
    ]
  },
  "Adapters": {
    "MockCrm": {
      "Enabled": true
    }
  }
}
```

### Enable Debug Logging

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug"
    }
  }
}
```

## Troubleshooting

### Connection Errors

**Problem:** `Failed to initialize adapter: AzureDevOps`

**Solution:**
- Verify your PAT token is valid and not expired
- Check the Organization URL is correct
- Ensure the PAT has "Work Items (Read & Write)" permissions

### Authentication Errors

**Problem:** `401 Unauthorized` errors

**Solution:**
- Double-check API credentials in appsettings.json
- Verify ServiceDesk Plus API is enabled
- Check that Technician Key is correct

### No Items Syncing

**Problem:** Sync runs but no items are synced

**Solution:**
- Check that work items exist in source system
- Verify items were modified within the sync window (default: last 30 days)
- Review field mappings in configuration

## Next Steps

- Read the [Development Guide](DEVELOPMENT.md) to add custom adapters
- Check the [README](README.md) for architecture details
- Review [Implementation Summary](IMPLEMENTATION_SUMMARY.md) for complete feature list

## Production Deployment

For production use, see the deployment section in [README.md](README.md#running-as-a-service).

## Getting Help

- Check logs for detailed error messages
- Review configuration file for typos
- Ensure all prerequisites are installed
- Verify network connectivity to both systems

## Success!

Once configured, the Sync Bridge will automatically:
- ✅ Detect new and updated work items
- ✅ Sync them between systems
- ✅ Keep comments synchronized
- ✅ Handle bidirectional updates
- ✅ Run continuously in the background

Enjoy seamless cross-system synchronization! 🎉
