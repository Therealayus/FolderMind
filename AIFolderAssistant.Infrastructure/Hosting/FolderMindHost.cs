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

namespace AIFolderAssistant.Infrastructure.Hosting;

/// <summary>
/// Shared composition root for every FolderMind host (WinUI app, CLI, future service).
/// Registers database, repositories, analysis pipeline, monitoring, AI, Windows
/// integration, and Serilog file (+optional console) logging.
/// UI layers add their own services (tray, notifications, viewmodels) on top.
/// </summary>
public static class FolderMindHost
{
    public static IServiceCollection CreateServices(bool consoleLog = false)
    {
        var services = new ServiceCollection();

        var logDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FolderMind", "logs");
        Directory.CreateDirectory(logDir);
        var serilog = new LoggerConfiguration().MinimumLevel.Information();
        if (consoleLog)
            serilog = serilog.WriteTo.Console();
        Log.Logger = serilog
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

        // AI providers (cloud only when explicitly enabled + configured).
        services.AddSingleton<MockAIProvider>();
        services.AddSingleton<ISecretStore, DpapiSecretStore>();
        services.AddHttpClient();
        services.AddSingleton<IAIProvider>(sp =>
        {
            var settings = sp.GetRequiredService<ISettingsService>();
            settings.Load();
            if (settings.Current.AI is { Enabled: true, AllowCloud: true } ai
                && !string.IsNullOrWhiteSpace(ai.Endpoint))
            {
                var http = sp.GetRequiredService<IHttpClientFactory>().CreateClient("ai");
                http.Timeout = TimeSpan.FromSeconds(30);
                var secrets = sp.GetRequiredService<ISecretStore>();
                var log = sp.GetRequiredService<ILogger<OpenAiCompatibleProvider>>();
                return new OpenAiCompatibleProvider(http, log, ai.Endpoint,
                    string.IsNullOrWhiteSpace(ai.Model) ? "gpt-4o-mini" : ai.Model,
                    () => secrets.GetSecret("ai-api-key"));
            }
            return sp.GetRequiredService<MockAIProvider>();
        });

        // File-system monitoring (debounce read from persisted settings at first resolve).
        services.AddSingleton<IFileSystemMonitor, FileSystemMonitor>();
        services.AddSingleton<FolderWatchService>(sp =>
        {
            var settings = sp.GetRequiredService<ISettingsService>();
            settings.Load();
            return new FolderWatchService(
                sp.GetRequiredService<IFileSystemMonitor>(),
                sp.GetRequiredService<ILogger<FolderWatchService>>(),
                debounceWindow: TimeSpan.FromSeconds(Math.Clamp(settings.Current.Monitoring.DebounceSeconds, 1, 60)));
        });

        // Windows integration (no admin required).
        services.AddSingleton<IStartupService, StartupService>();

        return services;
    }

    private sealed class NoOpUpdateService : IUpdateService
    {
        public Task<UpdateInfo?> CheckForUpdatesAsync(CancellationToken ct) => Task.FromResult<UpdateInfo?>(null);
    }
}
