using Microsoft.Extensions.Options;
using SyncBridge.Adapters.AzureDevOps;
using SyncBridge.Adapters.Crm.Mock;
using SyncBridge.Adapters.ServiceDeskPlus;
using SyncBridge.Core.Configuration;
using SyncBridge.Core.Interfaces;
using SyncBridge.Core.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();

// Configure configuration sections
builder.Services.Configure<SyncConfiguration>(builder.Configuration.GetSection("Sync"));
builder.Services.Configure<AdapterConfiguration>(builder.Configuration.GetSection("Adapters"));

// Register core services
builder.Services.AddSingleton<SyncEngine>();

// Register adapters
builder.Services.AddHttpClient<ServiceDeskPlusAdapter>();
builder.Services.AddSingleton<ISyncAdapter, AzureDevOpsAdapter>();
builder.Services.AddSingleton<ISyncAdapter, ServiceDeskPlusAdapter>();
builder.Services.AddSingleton<ISyncAdapter, MockCrmAdapter>();

// Add session support for storing temporary data
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseSession();

app.UseAuthorization();

app.MapRazorPages();

app.Run();
