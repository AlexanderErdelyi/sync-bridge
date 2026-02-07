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

## Step 2: Run the Web Interface

The easiest way to get started is with the web interface:

```bash
cd SyncBridge.Web
dotnet run
```

Then open your browser and navigate to **https://localhost:5001** (or http://localhost:5000).

## Step 3: Configure Through the Web UI

### Azure DevOps Configuration

1. Click **Configuration** in the navigation menu
2. In the **Azure DevOps** section, enter:
   - **Organization URL**: `https://dev.azure.com/YOUR-ORG`
   - **Personal Access Token (PAT)**: Your PAT token
   - **Project**: Your project name
3. Click **Save Azure DevOps Config**

#### Getting Azure DevOps PAT

1. Go to https://dev.azure.com/YOUR-ORG
2. Click User Settings (top right) → Personal Access Tokens
3. Create new token with "Work Items (Read & Write)" scope
4. Copy the token (you won't see it again!)

### ServiceDesk Plus Configuration

1. In the **ServiceDesk Plus** section, enter:
   - **Base URL**: `https://your-instance.servicedeskplus.com`
   - **API Key**: Your API key
   - **Technician Key**: Your technician key
2. Click **Save ServiceDesk Config**

#### Getting ServiceDesk Plus API Keys

1. Log into ServiceDesk Plus as admin
2. Go to Admin → API → Generate API Key
3. Copy both API Key and Technician Key

### Sync Configuration

1. In the **Sync Configuration** section:
   - **Poll Interval**: 60 seconds (how often to check for changes)
   - **Batch Size**: 100 (max items per sync)
   - **Sync Comments**: ✓ Enabled
2. Click **Save Sync Config**

## Step 4: Test Your Configuration

1. Navigate to the **Testing** page
2. Click **Test All Connections** to verify adapters can connect
3. Select an adapter and click **Retrieve Work Items** to see recent items
4. Click **Test Sync** to perform a test synchronization

## Step 5: Create and Sync Work Items

### Create a Work Item

1. Navigate to the **Work Items** page
2. Click **Create Work Item**
3. Fill in the details:
   - Select target system (AzureDevOps, ServiceDeskPlus, or MockCRM)
   - Enter title: "Test Work Item"
   - Enter description
   - Set type, priority, and state
4. Check **Automatically sync to other systems**
5. Click **Create & Sync**

The work item will be created and automatically synced to other configured systems!

### Add Comments

1. Click **View** on any work item
2. Scroll to the **Comments** section
3. Enter your name and comment
4. Check **Sync comment to other systems**
5. Click **Add Comment & Sync**

The comment will be synced to all configured systems.

## Step 6: Monitor Sync Status

Return to the **Dashboard** to:
- View last sync results
- See active adapters and mappings
- Trigger manual synchronization with **Trigger Manual Sync** button

## Alternative: CLI/Background Service

For running as a background service without the web interface:

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
