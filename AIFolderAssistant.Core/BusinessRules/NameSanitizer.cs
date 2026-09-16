namespace AIFolderAssistant.Core.BusinessRules;

/// <summary>
/// Sanitizes AI/local folder-name suggestions so they are always safe on Windows.
/// Strips invalid characters, reserved names, trailing dots/spaces, and enforces length limits.
/// </summary>
public static class NameSanitizer
{
    private static readonly char[] InvalidChars = new[] { '<', '>', ':', '"', '/', '\\', '|', '?', '*' };
    private static readonly HashSet<string> ReservedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
    };

    public const int MaxNameLength = 100;

    public static string Sanitize(string? raw, string fallback = "Untitled Folder")
    {
        if (string.IsNullOrWhiteSpace(raw))
            return fallback;

        var name = raw.Trim();

        foreach (var c in InvalidChars)
            name = name.Replace(c, ' ');
        foreach (var c in Path.GetInvalidFileNameChars().Distinct())
            name = name.Replace(c, ' ');

        // Collapse whitespace, trim trailing dots/spaces (illegal on Windows).
        name = string.Join(' ', name.Split((char[])null!, StringSplitOptions.RemoveEmptyEntries));
        name = name.Trim().TrimEnd('.', ' ');
        // Remove emojis / non-printables outside basic multilingual plane text range.
        name = new string(name.Where(ch => !char.IsControl(ch) && (ch < 0xD800 || ch > 0xDFFF)).ToArray()).Trim();

        if (name.Length > MaxNameLength)
            name = name.Substring(0, MaxNameLength).TrimEnd('.', ' ');

        if (string.IsNullOrWhiteSpace(name))
            return fallback;
        if (ReservedNames.Contains(name))
            return $"{name} Folder";
        return name;
    }
}
