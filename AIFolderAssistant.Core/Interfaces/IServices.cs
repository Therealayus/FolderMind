using AIFolderAssistant.Core.Configuration;

namespace AIFolderAssistant.Core.Interfaces;

/// <summary>Key/value settings store abstraction (SQLite-backed in production).</summary>
public interface ISettingsStore
{
    string? Get(string key);
    void Set(string key, string value);
}

/// <summary>Strongly-typed settings service with defaults and change notification.</summary>
public interface ISettingsService
{
    AppSettings Current { get; }
    void Load();
    void Save();
    void ResetToDefaults();
    event EventHandler<AppSettings>? Changed;
}

/// <summary>Secure per-user secret storage (DPAPI-backed; never plain text).</summary>
public interface ISecretStore
{
    void SetSecret(string name, string value);
    string? GetSecret(string name);
    void DeleteSecret(string name);
}

/// <summary>Future updater extension point (GitHub Releases / Store / private server).</summary>
public interface IUpdateService
{
    Task<UpdateInfo?> CheckForUpdatesAsync(CancellationToken ct);
}

public sealed class UpdateInfo
{
    public string Version { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public string DownloadUrl { get; set; } = string.Empty;
}

/// <summary>Safe folder-rename workflow with history + undo support.</summary>
public interface IRenameService
{
    Task<RenameResult> RenameAsync(string folderPath, string newName, CancellationToken ct);
    Task<UndoResult> UndoAsync(string folderPath, CancellationToken ct);
}

public sealed class RenameResult
{
    public bool Success { get; set; }
    public string? NewPath { get; set; }
    public string? ErrorMessage { get; set; }
}

public sealed class UndoResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>Windows toast notifications abstraction.</summary>
public interface INotificationService
{
    bool CanNotify(string kind);
    void NotifySuggestion(string folderPath, string originalName, string suggestedName, double confidence, string reason);
    void NotifyInfo(string title, string message);
}

/// <summary>Start-with-Windows abstraction (registry Run key for unpackaged apps).</summary>
public interface IStartupService
{
    bool IsEnabled();
    void SetEnabled(bool enabled);
}

/// <summary>System-tray controller abstraction (status, pause/resume, open, exit).</summary>
public interface ITrayController : IDisposable
{
    bool MonitoringActive { get; }
    void SetMonitoringActive(bool active);
    event EventHandler? OpenRequested;
    event EventHandler? PauseRequested;
    event EventHandler? ResumeRequested;
    event EventHandler? ExitRequested;
    event EventHandler? SettingsRequested;
    event EventHandler? HistoryRequested;
}
