using AIFolderAssistant.Core.AI;
using AIFolderAssistant.Core.Analysis;
using AIFolderAssistant.Core.BusinessRules;
using AIFolderAssistant.Core.Engines;
using AIFolderAssistant.Core.Interfaces;
using AIFolderAssistant.Core.Services;
using AIFolderAssistant.Infrastructure.AI;
using AIFolderAssistant.Infrastructure.Database;
using AIFolderAssistant.Infrastructure.FileSystem;
using AIFolderAssistant.Infrastructure.Repository;
using AIFolderAssistant.Infrastructure.Security;
using AIFolderAssistant.Infrastructure.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;

namespace AIFolderAssistant.App.Services;

/// <summary>
/// Composition root: registers database, repositories, analyzer pipeline,
/// file-system monitoring, AI providers, Windows integration, and logging.
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
                return _provider ??= Build();
            }
        }
    }

    public static T Get<T>() where T : notnull => Provider.GetRequiredService<T>();

    private static ServiceProvider Build()
    {
        var services = new ServiceCollection();

        // Logging → %AppData%\FolderMind\logs\foldermind-.log (no file contents logged).
        var logDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FolderMind", "logs");
        Directory.CreateDirectory(logDir);
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(Path.Combine(logDir, "foldermind-.log"), rollingInterval: RollingInterval.Day, retainedFileCountLimit: 14)
            .CreateLogger();
        services.AddLogging(b => b.ClearProviders().AddSerilog(dispose: true));

        // Database + repositories.
        services.AddSingleton<FolderMindDbContext>();
        services.AddSingleton<IMonitoredFolderRepository, MonitoredFolderRepository>();
        services.AddSingleton<IFolderAnalysisRepository, FolderAnalysisRepository>();
        services.AddSingleton<ISuggestionRepository, SuggestionRepository>();
        services.AddSingleton<IRenameHistoryRepository, RenameHistoryRepository>();
        services.AddSingleton<ISettingsRepository, SettingsRepository>();
        services.AddSingleton<IAIUsageRepository, AIUsageRepository>();
        services.AddSingleton<IDiagnosticRepository, DiagnosticRepository>();

        // Core bridges + services.
        services.AddSingleton<ISettingsStore, SettingsStoreAdapter>();
        services.AddSingleton<IRenameHistoryRecorder, RenameHistoryRecorderAdapter>();
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<IRenameService, RenameService>();
        services.AddSingleton<IUpdateService, NoOpUpdateService>();

        // Analysis pipeline (local-first, always available offline).
        services.AddSingleton<IMetadataExtractor, SimpleMetadataExtractor>();
        services.AddSingleton<IContentExtractor, SimpleContentExtractor>();
        services.AddSingleton<IContentClassifier, KeywordContentClassifier>();
        services.AddSingleton<IFolderNameGenerator, FolderNameGenerator>();
        services.AddSingleton<IConfidenceCalculator, ConfidenceCalculator>();
        services.AddSingleton<IFolderAnalyzer, LocalFolderAnalyzer>();
        services.AddSingleton<IFolderAnalysisPipeline, LocalAnalysisPipeline>();
        services.AddSingleton<SuggestionService>();

        // AI providers.
        services.AddSingleton<MockAIProvider>();
        services.AddSingleton<ISecretStore, DpapiSecretStore>();
        services.AddHttpClient();
        services.AddSingleton<IAIProvider>(sp =>
        {
            var settings = sp.GetRequiredService<ISettingsService>();
            settings.Load();
            // Cloud provider only when explicitly enabled + configured; otherwise mock (offline).
            if (settings.Current.AI is { Enabled: true, AllowCloud: true } ai
                && !string.IsNullOrWhiteSpace(ai.Endpoint))
            {
                var http = sp.GetRequiredService<IHttpClientFactory>().CreateClient("ai");
                http.Timeout = TimeSpan.FromSeconds(30);
                var secrets = sp.GetRequiredService<ISecretStore>();
                var log = sp.GetRequiredService<ILogger<OpenAiCompatibleProvider>>();
                return new OpenAiCompatibleProvider(http, log, ai.Endpoint, string.IsNullOrWhiteSpace(ai.Model) ? "gpt-4o-mini" : ai.Model, () => secrets.GetSecret("ai-api-key"));
            }
            return sp.GetRequiredService<MockAIProvider>();
        });

        // File-system monitoring.
        services.AddSingleton<IFileSystemMonitor, FileSystemMonitor>();
        services.AddSingleton<FolderWatchService>(sp => new FolderWatchService(
            sp.GetRequiredService<IFileSystemMonitor>(),
            sp.GetRequiredService<ILogger<FolderWatchService>>(),
            debounceWindow: TimeSpan.FromSeconds(Math.Max(1, sp.GetRequiredService<ISettingsService>().Current.Monitoring.DebounceSeconds))));

        // Windows integration.
        services.AddSingleton<IStartupService, StartupService>();
        services.AddSingleton<INotificationService, TrayBalloonNotificationService>();
        services.AddSingleton<ITrayController, TrayController>();

        // ViewModels (transient — fresh state per navigation).
        services.AddTransient<ViewModels.HomeViewModel>();
        services.AddTransient<ViewModels.FoldersViewModel>();
        services.AddTransient<ViewModels.SuggestionsViewModel>();
        services.AddTransient<ViewModels.HistoryViewModel>();
        services.AddTransient<ViewModels.SettingsViewModel>();

        return services.BuildServiceProvider();
    }

    private sealed class NoOpUpdateService : IUpdateService
    {
        public Task<UpdateInfo?> CheckForUpdatesAsync(CancellationToken ct) => Task.FromResult<UpdateInfo?>(null);
    }
}
