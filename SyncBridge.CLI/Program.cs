using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SyncBridge.Adapters.AzureDevOps;
using SyncBridge.Adapters.Crm.Mock;
using SyncBridge.Adapters.ServiceDeskPlus;
using SyncBridge.Core.Configuration;
using SyncBridge.Core.Interfaces;
using SyncBridge.Core.Services;

var builder = Host.CreateApplicationBuilder(args);

// Configure services
builder.Configuration
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables()
    .AddCommandLine(args);

// Configure logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.SetMinimumLevel(LogLevel.Information);

// Register configuration
builder.Services.Configure<SyncConfiguration>(builder.Configuration.GetSection("Sync"));
builder.Services.Configure<AdapterConfiguration>(builder.Configuration.GetSection("Adapters"));

// Register core services
builder.Services.AddSingleton<SyncEngine>();

// Register adapters
builder.Services.AddHttpClient<ServiceDeskPlusAdapter>();
builder.Services.AddSingleton<ISyncAdapter, AzureDevOpsAdapter>();
builder.Services.AddSingleton<ISyncAdapter, ServiceDeskPlusAdapter>();
builder.Services.AddSingleton<ISyncAdapter, MockCrmAdapter>();

// Register background service
builder.Services.AddHostedService<SyncBackgroundService>();

var host = builder.Build();

Console.WriteLine("╔════════════════════════════════════════════╗");
Console.WriteLine("║        Sync Bridge Service v1.0           ║");
Console.WriteLine("║  Bidirectional Sync Platform for          ║");
Console.WriteLine("║  Azure DevOps & ServiceDesk Plus          ║");
Console.WriteLine("╚════════════════════════════════════════════╝");
Console.WriteLine();

await host.RunAsync();
