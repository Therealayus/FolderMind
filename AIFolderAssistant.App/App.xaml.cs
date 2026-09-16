using AIFolderAssistant.App.Services;
using AIFolderAssistant.Core.Analysis;
using AIFolderAssistant.Core.BusinessRules;
using AIFolderAssistant.Core.Interfaces;
using AIFolderAssistant.Infrastructure.Database;
using AIFolderAssistant.Infrastructure.FileSystem;
using AIFolderAssistant.Infrastructure.Repository;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;

namespace AIFolderAssistant.App;

/// <summary>
/// Application orchestrator: DI, tray, background folder watching,
/// settle → analyze → suggest → notify pipeline, and lifecycle
/// (hide-to-tray; only Exit terminates).
/// </summary>
public partial class App : Application
{
    public static MainWindow? MainWin { get; private set; }
    public static ITrayController? Tray { get; private set; }
    public static bool ExitRequested { get; private set; }

    private ILogger<App>? _logger;

    public App()
    {
        InitializeComponent();
    }

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        var services = AppServices.Provider;
        _logger = services.GetRequiredService<ILogger<App>>();

        var settings = services.GetRequiredService<ISettingsService>();
        settings.Load();

        MainWin = new MainWindow();

        // System tray (must be created on the UI thread).
        var tray = (TrayController)services.GetRequiredService<ITrayController>();
        Tray = tray;
        tray.Initialize(MainWin);
        tray.OpenRequested += (_, _) => ShowMain();
        tray.SettingsRequested += (_, _) => { ShowMain(); MainWin.Navigate("settings"); };
        tray.HistoryRequested += (_, _) => { ShowMain(); MainWin.Navigate("history"); };
        tray.PauseRequested += (_, _) => SetPaused(true);
        tray.ResumeRequested += (_, _) => SetPaused(false);
        tray.ExitRequested += (_, _) => ExitApp();

        // Background monitoring.
        var folders = services.GetRequiredService<IMonitoredFolderRepository>();
        var watch = services.GetRequiredService<FolderWatchService>();
        watch.FolderSettled += OnFolderSettled;
        var enabled = settings.Current.Monitoring.Enabled
            ? folders.GetAll().Where(f => f.IsEnabled).Select(f => f.Path).Distinct().ToList()
            : new List<string>();
        watch.Start(enabled);
        tray.SetMonitoringActive(settings.Current.Monitoring.Enabled && enabled.Count > 0);

        // Command line: --minimized (startup) or --analyze "<path>" (context menu).
        var cmdArgs = Environment.GetCommandLineArgs();
        var analyzeIdx = Array.FindIndex(cmdArgs, a => a.Equals("--analyze", StringComparison.OrdinalIgnoreCase));
        if (analyzeIdx >= 0 && analyzeIdx + 1 < cmdArgs.Length)
        {
            _ = AnalyzeNowAsync(cmdArgs[analyzeIdx + 1]);
            MainWin.Activate();
        }
        else if (!cmdArgs.Any(a => a.Equals("--minimized", StringComparison.OrdinalIgnoreCase)))
        {
            MainWin.Activate();
        }
        // --minimized: stay in tray quietly.

        _logger.LogInformation("FolderMind AI launched.");
        await Task.CompletedTask;
    }

    private static void ShowMain()
    {
        if (MainWin is null)
            return;
        MainWin.Show();
        MainWin.Activate();
    }

    private void SetPaused(bool paused)
    {
        try
        {
            var services = AppServices.Provider;
            var settings = services.GetRequiredService<ISettingsService>();
            settings.Current.Monitoring.Enabled = !paused;
            settings.Save();
            ((TrayController?)Tray)?.SetMonitoringActive(!paused);
            services.GetRequiredService<INotificationService>()
                .NotifyInfo("FolderMind AI", paused ? "Monitoring paused." : "Monitoring resumed.");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Pause/resume failed.");
        }
    }

    private async void OnFolderSettled(object? sender, string folder)
    {
        try
        {
            var services = AppServices.Provider;
            var settings = services.GetRequiredService<ISettingsService>();
            if (!settings.Current.Monitoring.Enabled)
                return;

            // Record scan time + persist analysis.
            var folders = services.GetRequiredService<IMonitoredFolderRepository>();
            var match = folders.GetAll()
                .Where(f => folder.StartsWith(f.Path, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(f => f.Path.Length)
                .FirstOrDefault();
            if (match is not null)
            {
                match.LastScannedAt = DateTime.UtcNow;
                match.UpdatedAt = DateTime.UtcNow;
                folders.Update(match);
            }

            var pipeline = services.GetRequiredService<IFolderAnalysisPipeline>();
            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
            var result = await pipeline.RunPipelineAsync(folder, cts.Token);

            var analyses = services.GetRequiredService<IFolderAnalysisRepository>();
            analyses.Add(new FolderAnalysis
            {
                MonitoredFolderId = match?.Id ?? 0,
                FolderPath = folder,
                OriginalName = Path.GetFileName(folder.TrimEnd(Path.DirectorySeparatorChar)) ?? folder,
                SuggestedName = result.SuggestedName,
                Confidence = result.Confidence,
                Reason = result.Reason,
                FileCount = result.FileCount,
                ProcessedFileCount = result.FileCount,
                CreatedAt = DateTime.UtcNow
            });

            // Only surface suggestions above the configured threshold. Never auto-rename.
            if (result.Confidence < settings.Current.Monitoring.MinimumConfidence)
                return;

            var sanitized = NameSanitizer.Sanitize(result.SuggestedName);
            var suggestions = services.GetRequiredService<ISuggestionRepository>();
            var latest = suggestions.GetLatest(folder);
            if (latest?.SuggestedName == sanitized && latest.Status == SuggestionStatus.Pending)
                return; // Avoid duplicate notifications for unchanged contents.

            suggestions.Add(new Suggestion
            {
                FolderPath = folder,
                OriginalName = Path.GetFileName(folder.TrimEnd(Path.DirectorySeparatorChar)) ?? folder,
                SuggestedName = sanitized,
                Confidence = result.Confidence,
                Reason = result.Reason,
                Status = SuggestionStatus.Pending,
                CreatedAt = DateTime.UtcNow
            });

            services.GetRequiredService<INotificationService>().NotifySuggestion(
                folder,
                Path.GetFileName(folder.TrimEnd(Path.DirectorySeparatorChar)) ?? folder,
                sanitized, result.Confidence, result.Reason);
        }
        catch (Exception ex)
        {
            // A filesystem event must never bring down the application.
            _logger?.LogError(ex, "Folder-settled handling failed for {Folder}.", folder);
        }
    }

    private async Task AnalyzeNowAsync(string folder)
    {
        try
        {
            var services = AppServices.Provider;
            var pipeline = services.GetRequiredService<IFolderAnalysisPipeline>();
            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
            var result = await pipeline.RunPipelineAsync(folder, cts.Token);
            services.GetRequiredService<INotificationService>().NotifySuggestion(
                folder,
                Path.GetFileName(folder.TrimEnd(Path.DirectorySeparatorChar)) ?? folder,
                NameSanitizer.Sanitize(result.SuggestedName), result.Confidence, result.Reason);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Manual analysis failed for {Folder}.", folder);
        }
    }

    private static void ExitApp()
    {
        ExitRequested = true;
        (Tray as IDisposable)?.Dispose();
        Environment.Exit(0);
    }
}
