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
    private readonly IWebHostEnvironment _environment;

    public ConfigurationModel(
        ILogger<ConfigurationModel> logger,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        _logger = logger;
        _configuration = configuration;
        _environment = environment;
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
        // Try to load from session first, fall back to appsettings.json
        var azureConfigJson = HttpContext.Session.GetString("AzureDevOpsConfig");
        if (!string.IsNullOrEmpty(azureConfigJson))
        {
            AzureDevOpsConfig = JsonSerializer.Deserialize<AzureDevOpsConfig>(azureConfigJson);
        }
        else
        {
            AzureDevOpsConfig = _configuration.GetSection("Adapters:AzureDevOps").Get<AzureDevOpsConfig>();
        }

        var serviceDeskJson = HttpContext.Session.GetString("ServiceDeskConfig");
        if (!string.IsNullOrEmpty(serviceDeskJson))
        {
            ServiceDeskConfig = JsonSerializer.Deserialize<ServiceDeskPlusConfig>(serviceDeskJson);
        }
        else
        {
            ServiceDeskConfig = _configuration.GetSection("Adapters:ServiceDeskPlus").Get<ServiceDeskPlusConfig>();
        }

        var syncConfigJson = HttpContext.Session.GetString("SyncConfig");
        if (!string.IsNullOrEmpty(syncConfigJson))
        {
            SyncConfig = JsonSerializer.Deserialize<SyncConfiguration>(syncConfigJson);
        }
        else
        {
            SyncConfig = _configuration.GetSection("Sync").Get<SyncConfiguration>();
        }
    }

    public IActionResult OnPostSaveAzureDevOps(string organizationUrl, string personalAccessToken, string project)
    {
        try
        {
            var config = new AzureDevOpsConfig
            {
                OrganizationUrl = organizationUrl,
                PersonalAccessToken = personalAccessToken,
                Project = project
            };

            // Save to session
            var json = JsonSerializer.Serialize(config);
            HttpContext.Session.SetString("AzureDevOpsConfig", json);

            // Save to appsettings.json
            SaveToAppSettings("Adapters:AzureDevOps", config);

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

            // Save to session
            var json = JsonSerializer.Serialize(config);
            HttpContext.Session.SetString("ServiceDeskConfig", json);

            // Save to appsettings.json
            SaveToAppSettings("Adapters:ServiceDeskPlus", config);

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

            // Save to session
            var json = JsonSerializer.Serialize(config);
            HttpContext.Session.SetString("SyncConfig", json);

            // Note: Sync config has Mappings array that we don't want to overwrite
            // So we only update the simple properties
            var appSettingsPath = Path.Combine(_environment.ContentRootPath, "appsettings.json");
            if (System.IO.File.Exists(appSettingsPath))
            {
                var jsonContent = System.IO.File.ReadAllText(appSettingsPath);
                var appSettings = JsonSerializer.Deserialize<Dictionary<string, object>>(jsonContent);
                if (appSettings != null && appSettings.ContainsKey("Sync"))
                {
                    var syncSection = JsonSerializer.Deserialize<Dictionary<string, object>>(appSettings["Sync"].ToString() ?? "{}");
                    if (syncSection != null)
                    {
                        syncSection["PollIntervalSeconds"] = pollIntervalSeconds;
                        syncSection["BatchSize"] = batchSize;
                        syncSection["SyncComments"] = syncComments;
                        appSettings["Sync"] = syncSection;

                        var options = new JsonSerializerOptions { WriteIndented = true };
                        System.IO.File.WriteAllText(appSettingsPath, JsonSerializer.Serialize(appSettings, options));
                    }
                }
            }

            TempData["Success"] = "Sync configuration saved successfully!";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving sync configuration");
            TempData["Error"] = $"Error saving configuration: {ex.Message}";
        }

        return RedirectToPage();
    }

    private void SaveToAppSettings(string sectionPath, object config)
    {
        try
        {
            var appSettingsPath = Path.Combine(_environment.ContentRootPath, "appsettings.json");
            if (System.IO.File.Exists(appSettingsPath))
            {
                var jsonContent = System.IO.File.ReadAllText(appSettingsPath);
                var appSettings = JsonSerializer.Deserialize<Dictionary<string, object>>(jsonContent);
                
                if (appSettings != null)
                {
                    var sections = sectionPath.Split(':');
                    Dictionary<string, object>? currentSection = appSettings;

                    for (int i = 0; i < sections.Length - 1; i++)
                    {
                        if (!currentSection.ContainsKey(sections[i]))
                        {
                            currentSection[sections[i]] = new Dictionary<string, object>();
                        }
                        var sectionValue = currentSection[sections[i]].ToString();
                        currentSection = JsonSerializer.Deserialize<Dictionary<string, object>>(sectionValue ?? "{}");
                    }

                    if (currentSection != null)
                    {
                        var lastSection = sections[sections.Length - 1];
                        
                        // Navigate back to set the value
                        if (sections.Length == 2)
                        {
                            var parentSection = JsonSerializer.Deserialize<Dictionary<string, object>>(appSettings[sections[0]].ToString() ?? "{}");
                            if (parentSection != null)
                            {
                                parentSection[lastSection] = config;
                                appSettings[sections[0]] = parentSection;
                            }
                        }

                        var options = new JsonSerializerOptions { WriteIndented = true };
                        System.IO.File.WriteAllText(appSettingsPath, JsonSerializer.Serialize(appSettings, options));
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not save to appsettings.json, using session only");
        }
    }
}
