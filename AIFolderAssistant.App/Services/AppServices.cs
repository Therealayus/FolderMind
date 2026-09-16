using AIFolderAssistant.Core.Interfaces;
using AIFolderAssistant.Infrastructure.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace AIFolderAssistant.App.Services;

/// <summary>
/// WinUI composition root: shared host services plus UI-only services
/// (tray, balloon notifications, viewmodels). Must be accessed on the UI thread.
/// </summary>
public static class AppServices
{
    private static ServiceProvider? _provider;
    private static readonly object Gate = new();

    public static ServiceProvider Provider
    {
        get
        {
            lock (Gate)
            {
                if (_provider is not null)
                    return _provider;

                var services = FolderMindHost.CreateServices(consoleLog: false);

                // UI-only services.
                services.AddSingleton<INotificationService, TrayBalloonNotificationService>();
                services.AddSingleton<ITrayController, TrayController>();

                // ViewModels (transient — fresh state per navigation).
                services.AddTransient<ViewModels.HomeViewModel>();
                services.AddTransient<ViewModels.FoldersViewModel>();
                services.AddTransient<ViewModels.SuggestionsViewModel>();
                services.AddTransient<ViewModels.HistoryViewModel>();
                services.AddTransient<ViewModels.SettingsViewModel>();

                _provider = services.BuildServiceProvider();
                return _provider;
            }
        }
    }

    public static T Get<T>() where T : notnull => Provider.GetRequiredService<T>();
}
