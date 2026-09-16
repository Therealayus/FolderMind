namespace AIFolderAssistant.Core.BusinessRules;

/// <summary>
/// Validates file-system paths: traversal protection, system-directory guard,
/// length limits, and reserved-name checks.
/// </summary>
public static class PathValidator
{
    private static readonly string[] SystemPrefixes = new[]
    {
        @"C:\Windows",
        @"C:\Program Files",
        @"C:\Program Files (x86)",
        @"C:\ProgramData",
        Environment.GetFolderPath(Environment.SpecialFolder.System),
        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
    }.Where(p => !string.IsNullOrEmpty(p)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

    public static bool IsSystemDirectory(string path)
    {
        var full = GetFullPathSafe(path);
        if (full is null)
            return true;
        return SystemPrefixes.Any(prefix =>
            full.Equals(prefix, StringComparison.OrdinalIgnoreCase) ||
            full.StartsWith(prefix + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));
    }

    public static (bool Ok, string? Error) ValidateFolderPath(string? path, bool mustExist = false)
    {
        if (string.IsNullOrWhiteSpace(path))
            return (false, "Folder path must not be empty.");
        if (path.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
            return (false, "Folder path contains invalid characters.");
        string full;
        try
        {
            full = Path.GetFullPath(path);
        }
        catch (Exception ex)
        {
            return (false, $"Folder path is not valid: {ex.Message}");
        }
        if (full.Length > 240)
            return (false, "Folder path is too long.");
        if (mustExist && !Directory.Exists(full))
            return (false, "Folder does not exist.");
        if (IsSystemDirectory(full))
            return (false, "System directories cannot be monitored.");
        return (true, null);
    }

    public static (bool Ok, string? Error) ValidateNewFolderName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return (false, "Folder name must not be empty.");
        if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            return (false, "Folder name contains invalid characters.");
        if (name.Trim().EndsWith('.'))
            return (false, "Folder name must not end with a dot.");
        if (name.Length > NameSanitizer.MaxNameLength)
            return (false, $"Folder name must be at most {NameSanitizer.MaxNameLength} characters.");
        return (true, null);
    }

    private static string? GetFullPathSafe(string path)
    {
        try { return Path.GetFullPath(path); }
        catch { return null; }
    }
}
