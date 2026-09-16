namespace AIFolderAssistant.Core.Configuration;

/// <summary>
/// Strongly-typed application settings with production defaults.
/// Persisted via <see cref="Interfaces.ISettingsService"/> (SQLite-backed).
/// </summary>
public sealed class AppSettings
{
    public GeneralSettings General { get; set; } = new();
    public MonitoringSettings Monitoring { get; set; } = new();
    public AISettings AI { get; set; } = new();
    public PrivacySettings Privacy { get; set; } = new();
    public NotificationSettings Notifications { get; set; } = new();
    public AdvancedSettings Advanced { get; set; } = new();
}

public sealed class GeneralSettings
{
    public bool StartWithWindows { get; set; } = false;
    public bool ShowNotifications { get; set; } = true;
    public bool RunInBackground { get; set; } = true;
    public string Theme { get; set; } = "System"; // System | Light | Dark
}

public sealed class MonitoringSettings
{
    public bool Enabled { get; set; } = false;
    public int DebounceSeconds { get; set; } = 3;
    public double MinimumConfidence { get; set; } = 0.80;
    public int MaxFilesPerFolder { get; set; } = 500;
    public List<string> IgnoredFolderNames { get; set; } = new() { ".git", "node_modules", "bin", "obj", "build", "dist", ".cache", "temp" };
    public List<string> IgnoredExtensions { get; set; } = new();
}

public sealed class AISettings
{
    public bool Enabled { get; set; } = false;
    public string Provider { get; set; } = "None"; // None | OpenAICompatible | Local | Mock
    public string Model { get; set; } = string.Empty;
    public string Endpoint { get; set; } = string.Empty;
    public bool UseLocalFirst { get; set; } = true;
    public bool AllowCloud { get; set; } = false;
}

public sealed class PrivacySettings
{
    public bool LocalOnly { get; set; } = true;
    public bool StoreDocumentContents { get; set; } = false;
}

public sealed class NotificationSettings
{
    public bool EnableSuggestions { get; set; } = true;
    public int MaxPerHour { get; set; } = 10;
    public int QuietHoursStart { get; set; } = 22;
    public int QuietHoursEnd { get; set; } = 7;
}

public sealed class AdvancedSettings
{
    public string LogLevel { get; set; } = "Information";
    public bool DiagnosticsEnabled { get; set; } = true;
}
