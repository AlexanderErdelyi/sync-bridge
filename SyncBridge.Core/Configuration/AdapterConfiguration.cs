namespace SyncBridge.Core.Configuration;

/// <summary>
/// Configuration for adapter connections
/// </summary>
public class AdapterConfiguration
{
    /// <summary>
    /// Azure DevOps configuration
    /// </summary>
    public AzureDevOpsConfig? AzureDevOps { get; set; }

    /// <summary>
    /// Service Desk Plus configuration
    /// </summary>
    public ServiceDeskPlusConfig? ServiceDeskPlus { get; set; }

    /// <summary>
    /// Mock CRM configuration
    /// </summary>
    public MockCrmConfig? MockCrm { get; set; }
}

/// <summary>
/// Azure DevOps configuration
/// </summary>
public class AzureDevOpsConfig
{
    /// <summary>
    /// Organization URL
    /// </summary>
    public string OrganizationUrl { get; set; } = string.Empty;

    /// <summary>
    /// Personal Access Token
    /// </summary>
    public string PersonalAccessToken { get; set; } = string.Empty;

    /// <summary>
    /// Project name
    /// </summary>
    public string Project { get; set; } = string.Empty;
}

/// <summary>
/// Service Desk Plus configuration
/// </summary>
public class ServiceDeskPlusConfig
{
    /// <summary>
    /// Base URL
    /// </summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// API Key
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Technician Key
    /// </summary>
    public string TechnicianKey { get; set; } = string.Empty;
}

/// <summary>
/// Mock CRM configuration
/// </summary>
public class MockCrmConfig
{
    /// <summary>
    /// Whether to enable mock data
    /// </summary>
    public bool Enabled { get; set; } = true;
}
