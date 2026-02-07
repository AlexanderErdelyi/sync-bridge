using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using SyncBridge.Core.Configuration;
using System.Text.Json;

namespace SyncBridge.Web.Pages;

public class ConfigurationModel : PageModel
{
    private readonly ILogger<ConfigurationModel> _logger;
    private readonly IConfiguration _configuration;

    public ConfigurationModel(
        ILogger<ConfigurationModel> logger,
        IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    public AzureDevOpsConfig? AzureDevOpsConfig { get; set; }
    public ServiceDeskPlusConfig? ServiceDeskConfig { get; set; }
    public SyncConfiguration? SyncConfig { get; set; }

    public void OnGet()
    {
        LoadConfiguration();
    }

    private void LoadConfiguration()
    {
        AzureDevOpsConfig = _configuration.GetSection("Adapters:AzureDevOps").Get<AzureDevOpsConfig>();
        ServiceDeskConfig = _configuration.GetSection("Adapters:ServiceDeskPlus").Get<ServiceDeskPlusConfig>();
        SyncConfig = _configuration.GetSection("Sync").Get<SyncConfiguration>();
    }

    public IActionResult OnPostSaveAzureDevOps(string organizationUrl, string personalAccessToken, string project)
    {
        try
        {
            // Note: In a real application, you would save this to appsettings.json or a database
            // For this demo, we'll store in session
            var config = new AzureDevOpsConfig
            {
                OrganizationUrl = organizationUrl,
                PersonalAccessToken = personalAccessToken,
                Project = project
            };

            var json = JsonSerializer.Serialize(config);
            HttpContext.Session.SetString("AzureDevOpsConfig", json);

            TempData["Success"] = "Azure DevOps configuration saved successfully!";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving Azure DevOps configuration");
            TempData["Error"] = $"Error saving configuration: {ex.Message}";
        }

        return RedirectToPage();
    }

    public IActionResult OnPostSaveServiceDesk(string baseUrl, string apiKey, string technicianKey)
    {
        try
        {
            var config = new ServiceDeskPlusConfig
            {
                BaseUrl = baseUrl,
                ApiKey = apiKey,
                TechnicianKey = technicianKey
            };

            var json = JsonSerializer.Serialize(config);
            HttpContext.Session.SetString("ServiceDeskConfig", json);

            TempData["Success"] = "ServiceDesk Plus configuration saved successfully!";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving ServiceDesk configuration");
            TempData["Error"] = $"Error saving configuration: {ex.Message}";
        }

        return RedirectToPage();
    }

    public IActionResult OnPostSaveSyncConfig(int pollIntervalSeconds, int batchSize, bool syncComments)
    {
        try
        {
            var config = new SyncConfiguration
            {
                PollIntervalSeconds = pollIntervalSeconds,
                BatchSize = batchSize,
                SyncComments = syncComments
            };

            var json = JsonSerializer.Serialize(config);
            HttpContext.Session.SetString("SyncConfig", json);

            TempData["Success"] = "Sync configuration saved successfully!";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving sync configuration");
            TempData["Error"] = $"Error saving configuration: {ex.Message}";
        }

        return RedirectToPage();
    }
}
