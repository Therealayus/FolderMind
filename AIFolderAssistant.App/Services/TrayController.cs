using AIFolderAssistant.Core.Interfaces;
using H.NotifyIcon;
using Microsoft.UI.Xaml;

namespace AIFolderAssistant.App.Services;

/// <summary>
/// System-tray controller built on H.NotifyIcon (WinUI-compatible, unpackaged-safe).
/// Left-click opens the main window; right-click shows the full menu with live
/// Active/Paused status. Closing the main window hides it — only Exit terminates.
/// Must be created on the UI thread.
/// </summary>
public sealed class TrayController : ITrayController
{
    private TaskbarIcon? _icon;
    private bool _monitoringActive;
    private bool _disposed;

    public bool MonitoringActive => _monitoringActive;

    public event EventHandler? OpenRequested;
    public event EventHandler? PauseRequested;
    public event EventHandler? ResumeRequested;
    public event EventHandler? ExitRequested;
    public event EventHandler? SettingsRequested;
    public event EventHandler? HistoryRequested;

    public void Initialize(Window window)
    {
        _icon = new TaskbarIcon
        {
            ToolTipText = "FolderMind AI",
            ContextMenuMode = ContextMenuMode.SecondWindow,
            Visibility = Microsoft.UI.Xaml.Visibility.Visible
        };
        _icon.TrayLeftMouseDown += (_, _) => OpenRequested?.Invoke(this, EventArgs.Empty);
        RebuildMenu();
    }

    public void SetMonitoringActive(bool active)
    {
        _monitoringActive = active;
        RebuildMenu();
    }

    public void ShowBalloon(string title, string message)
    {
        try
        {
            _icon?.ShowNotification(title, message);
        }
        catch
        {
            // Tray balloons must never crash the app.
        }
    }

    private void RebuildMenu()
    {
        if (_icon is null)
            return;
        var status = _monitoringActive ? "● Active" : "○ Paused";
        var toggle = _monitoringActive ? "Pause Monitoring" : "Resume Monitoring";

        var menu = new Microsoft.UI.Xaml.Controls.MenuFlyout();
        AddItem(menu, "FolderMind AI", () => OpenRequested?.Invoke(this, EventArgs.Empty), enabled: true);
        menu.Items.Add(new Microsoft.UI.Xaml.Controls.MenuFlyoutSeparator());
        AddItem(menu, status, null, enabled: false);
        AddItem(menu, toggle, () =>
        {
            if (_monitoringActive) PauseRequested?.Invoke(this, EventArgs.Empty);
            else ResumeRequested?.Invoke(this, EventArgs.Empty);
        }, enabled: true);
        menu.Items.Add(new Microsoft.UI.Xaml.Controls.MenuFlyoutSeparator());
        AddItem(menu, "Open FolderMind", () => OpenRequested?.Invoke(this, EventArgs.Empty), enabled: true);
        AddItem(menu, "Settings", () => SettingsRequested?.Invoke(this, EventArgs.Empty), enabled: true);
        AddItem(menu, "History", () => HistoryRequested?.Invoke(this, EventArgs.Empty), enabled: true);
        menu.Items.Add(new Microsoft.UI.Xaml.Controls.MenuFlyoutSeparator());
        AddItem(menu, "Exit", () => ExitRequested?.Invoke(this, EventArgs.Empty), enabled: true);
        _icon.ContextFlyout = menu;
    }

    private static void AddItem(Microsoft.UI.Xaml.Controls.MenuFlyout menu, string text, Action? onClick, bool enabled)
    {
        var item = new Microsoft.UI.Xaml.Controls.MenuFlyoutItem { Text = text, IsEnabled = enabled };
        if (onClick is not null)
            item.Click += (_, _) => onClick();
        menu.Items.Add(item);
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _icon?.Dispose();
    }
}
