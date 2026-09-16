using AIFolderAssistant.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace AIFolderAssistant.App.Services;

/// <summary>
/// User notification service honoring settings: master switch, per-hour budget,
/// and quiet hours. Delivers via tray balloon (works unpackaged, no identity
/// requirements); WinUI AppNotifications remain a future packaged-build option.
/// </summary>
public sealed class TrayBalloonNotificationService : INotificationService
{
    private readonly ISettingsService _settings;
    private readonly ILogger<TrayBalloonNotificationService> _logger;
    private readonly Queue<DateTime> _recent = new();
    private readonly object _gate = new();

    public TrayBalloonNotificationService(ISettingsService settings, ILogger<TrayBalloonNotificationService> logger)
    {
        _settings = settings;
        _logger = logger;
    }

    public bool CanNotify(string kind)
    {
        var cfg = _settings.Current;
        if (!cfg.General.ShowNotifications)
            return false;
        if (kind == "suggestion" && !cfg.Notifications.EnableSuggestions)
            return false;

        var now = DateTime.Now;
        if (IsQuietHour(now, cfg.Notifications.QuietHoursStart, cfg.Notifications.QuietHoursEnd))
            return false;

        lock (_gate)
        {
            while (_recent.Count > 0 && now - _recent.Peek() > TimeSpan.FromHours(1))
                _recent.Dequeue();
            return _recent.Count < Math.Max(1, cfg.Notifications.MaxPerHour);
        }
    }

    public void NotifySuggestion(string folderPath, string originalName, string suggestedName, double confidence, string reason)
    {
        if (!CanNotify("suggestion"))
        {
            _logger.LogInformation("Suggestion notification suppressed by settings for {Folder}.", folderPath);
            return;
        }
        MarkSent();
        var band = confidence >= 0.9 ? "High" : confidence >= 0.8 ? "Medium" : "Low";
        var title = $"Better name for “{originalName}”";
        var message = $"{suggestedName} ({band} confidence)";
        try
        {
            (App.Tray as TrayController)?.ShowBalloon(title, message);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Balloon notification failed.");
        }
        _logger.LogInformation("Suggested {Suggestion} for {Folder} (confidence {Confidence}).", suggestedName, folderPath, confidence);
    }

    public void NotifyInfo(string title, string message)
    {
        if (!CanNotify("info"))
            return;
        MarkSent();
        try
        {
            (App.Tray as TrayController)?.ShowBalloon(title, message);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Balloon notification failed.");
        }
    }

    private void MarkSent()
    {
        lock (_gate) { _recent.Enqueue(DateTime.Now); }
    }

    private static bool IsQuietHour(DateTime now, int start, int end)
    {
        if (start == end)
            return false;
        return start < end
            ? now.Hour >= start && now.Hour < end
            : now.Hour >= start || now.Hour < end;
    }
}
