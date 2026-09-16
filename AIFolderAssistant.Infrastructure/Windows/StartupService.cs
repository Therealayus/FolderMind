using AIFolderAssistant.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace AIFolderAssistant.Infrastructure.Windows;

/// <summary>
/// Start-with-Windows via the per-user Run registry key
/// (<c>HKCU\Software\Microsoft\Windows\CurrentVersion\Run</c>).
/// No admin rights required; default OFF — only enabled on explicit user opt-in.
/// </summary>
public sealed class StartupService : IStartupService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "FolderMind AI";

    private readonly ILogger<StartupService> _logger;

    public StartupService(ILogger<StartupService> logger)
    {
        _logger = logger;
    }

    public bool IsEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
            return key?.GetValue(ValueName) is not null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not read startup setting.");
            return false;
        }
    }

    public void SetEnabled(bool enabled)
    {
        try
        {
            var exePath = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exePath))
                throw new InvalidOperationException("Cannot determine application path.");

            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true)
                ?? throw new InvalidOperationException("Cannot open startup registry key.");

            if (enabled)
            {
                key.SetValue(ValueName, $"\"{exePath}\" --minimized");
                _logger.LogInformation("Enabled start with Windows.");
            }
            else
            {
                key.DeleteValue(ValueName, throwOnMissingValue: false);
                _logger.LogInformation("Disabled start with Windows.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to change startup setting.");
            throw;
        }
    }
}
