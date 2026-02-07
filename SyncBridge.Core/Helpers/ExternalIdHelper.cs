namespace SyncBridge.Core.Helpers;

/// <summary>
/// Helper class for managing external ID format and operations
/// </summary>
public static class ExternalIdHelper
{
    /// <summary>
    /// Format for external IDs: "SystemName:ItemId"
    /// </summary>
    private const string ExternalIdFormat = "{0}:{1}";

    /// <summary>
    /// Creates an external ID from a system name and item ID
    /// </summary>
    /// <param name="systemName">The source system name</param>
    /// <param name="itemId">The item ID in the source system</param>
    /// <returns>Formatted external ID</returns>
    public static string CreateExternalId(string systemName, string itemId)
    {
        return string.Format(ExternalIdFormat, systemName, itemId);
    }

    /// <summary>
    /// Checks if an external ID starts with the given system name
    /// </summary>
    /// <param name="externalId">The external ID to check</param>
    /// <param name="systemName">The system name to check for</param>
    /// <returns>True if the external ID starts with the system name</returns>
    public static bool IsFromSystem(string? externalId, string systemName)
    {
        if (string.IsNullOrEmpty(externalId))
            return false;

        return externalId.StartsWith($"{systemName}:", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Extracts the system name from an external ID
    /// </summary>
    /// <param name="externalId">The external ID</param>
    /// <returns>The system name or null if format is invalid</returns>
    public static string? GetSystemName(string? externalId)
    {
        if (string.IsNullOrEmpty(externalId))
            return null;

        var parts = externalId.Split(':', 2);
        return parts.Length == 2 ? parts[0] : null;
    }

    /// <summary>
    /// Extracts the item ID from an external ID
    /// </summary>
    /// <param name="externalId">The external ID</param>
    /// <returns>The item ID or null if format is invalid</returns>
    public static string? GetItemId(string? externalId)
    {
        if (string.IsNullOrEmpty(externalId))
            return null;

        var parts = externalId.Split(':', 2);
        return parts.Length == 2 ? parts[1] : null;
    }
}
