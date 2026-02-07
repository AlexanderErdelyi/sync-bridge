using System.Text.RegularExpressions;

namespace SyncBridge.Web.Helpers;

/// <summary>
/// Helper class for text sanitization operations
/// </summary>
public static class TextHelper
{
    /// <summary>
    /// Strips HTML tags from a string and decodes HTML entities
    /// </summary>
    public static string StripHtmlTags(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return string.Empty;

        // Remove HTML tags
        var stripped = Regex.Replace(html, "<.*?>", string.Empty);
        
        // Decode common HTML entities
        stripped = System.Net.WebUtility.HtmlDecode(stripped);
        
        return stripped.Trim();
    }
}
