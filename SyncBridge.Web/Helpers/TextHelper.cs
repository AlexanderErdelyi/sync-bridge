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

        // Remove HTML tags - using a more robust pattern that handles attributes, self-closing tags, etc.
        var stripped = Regex.Replace(html, @"<[^>]+>|</[^>]+>", string.Empty, RegexOptions.Compiled);
        
        // Remove any remaining angle brackets as a defensive measure against malformed HTML
        // This may affect legitimate content like mathematical expressions (e.g., '5 < 10')
        // but is necessary to prevent display issues with broken HTML tags
        stripped = Regex.Replace(stripped, @"[<>]", string.Empty, RegexOptions.Compiled);
        
        // Decode common HTML entities
        stripped = System.Net.WebUtility.HtmlDecode(stripped);
        
        return stripped.Trim();
    }
}
