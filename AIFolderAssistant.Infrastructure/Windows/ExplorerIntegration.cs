using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace AIFolderAssistant.Infrastructure.Windows;

/// <summary>
/// Per-user Explorer context-menu integration (no admin required):
/// right-click a folder → "FolderMind AI → Suggest Folder Name" launches
/// the app with <c>--analyze "&lt;path&gt;"</c>.
/// Kept modular — Shell/COM integration can replace this without touching callers.
/// </summary>
public static class ExplorerIntegration
{
    private const string MenuKeyPath = @"Software\Classes\Directory\shell\FolderMindAI";
    private const string CommandKeyPath = @"Software\Classes\Directory\shell\FolderMindAI\command";
    private const string MenuText = "Suggest Folder Name with FolderMind AI";

    public static bool IsRegistered()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(CommandKeyPath, writable: false);
            return key?.GetValue(null) is not null;
        }
        catch
        {
            return false;
        }
    }

    public static void Register(ILogger? logger = null)
    {
        try
        {
            var exePath = Environment.ProcessPath
                ?? throw new InvalidOperationException("Cannot determine application path.");

            using (var menuKey = Registry.CurrentUser.CreateSubKey(MenuKeyPath))
            {
                menuKey?.SetValue(null, MenuText);
                menuKey?.SetValue("Icon", $"\"{exePath}\",0");
            }
            using (var cmdKey = Registry.CurrentUser.CreateSubKey(CommandKeyPath))
            {
                cmdKey?.SetValue(null, $"\"{exePath}\" --analyze \"%1\"");
            }
            logger?.LogInformation("Registered Explorer context menu.");
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Failed to register Explorer context menu.");
            throw;
        }
    }

    public static void Unregister(ILogger? logger = null)
    {
        try
        {
            Registry.CurrentUser.DeleteSubKeyTree(MenuKeyPath, throwOnMissingSubKey: false);
            logger?.LogInformation("Unregistered Explorer context menu.");
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Failed to unregister Explorer context menu.");
        }
    }
}
