using AIFolderAssistant.Core.Configuration;
using AIFolderAssistant.Core.Interfaces;
using AIFolderAssistant.Infrastructure.Database;
using AIFolderAssistant.Infrastructure.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace AIFolderAssistant.App.ViewModels;

public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly ISettingsService _settings;
    private readonly IStartupService _startup;
    private readonly ISecretStore _secrets;
    private readonly FolderMindDbContext _db;
    private readonly ILogger<SettingsViewModel> _logger;

    [ObservableProperty] private bool _startWithWindows;
    [ObservableProperty] private bool _showNotifications = true;
    [ObservableProperty] private bool _runInBackground = true;
    [ObservableProperty] private string _theme = "System";
    [ObservableProperty] private bool _monitoringEnabled;
    [ObservableProperty] private int _debounceSeconds = 3;
    [ObservableProperty] private double _minimumConfidence = 0.80;
    [ObservableProperty] private bool _aiEnabled;
    [ObservableProperty] private string _aiProvider = "None";
    [ObservableProperty] private string _aiEndpoint = string.Empty;
    [ObservableProperty] private string _aiModel = string.Empty;
    [ObservableProperty] private bool _allowCloud;
    [ObservableProperty] private bool _localOnly = true;
    [ObservableProperty] private bool _contextMenuRegistered;
    [ObservableProperty] private string _message = string.Empty;

    public SettingsViewModel(
        ISettingsService settings,
        IStartupService startup,
        ISecretStore secrets,
        FolderMindDbContext db,
        ILogger<SettingsViewModel> logger)
    {
        _settings = settings;
        _startup = startup;
        _secrets = secrets;
        _db = db;
        _logger = logger;
    }

    [RelayCommand]
    private void Load()
    {
        Message = string.Empty;
        _settings.Load();
        var c = _settings.Current;
        StartWithWindows = _startup.IsEnabled();
        ShowNotifications = c.General.ShowNotifications;
        RunInBackground = c.General.RunInBackground;
        Theme = c.General.Theme;
        MonitoringEnabled = c.Monitoring.Enabled;
        DebounceSeconds = c.Monitoring.DebounceSeconds;
        MinimumConfidence = c.Monitoring.MinimumConfidence;
        AiEnabled = c.AI.Enabled;
        AiProvider = c.AI.Provider;
        AiEndpoint = c.AI.Endpoint;
        AiModel = c.AI.Model;
        AllowCloud = c.AI.AllowCloud;
        LocalOnly = c.Privacy.LocalOnly;
        ContextMenuRegistered = ExplorerIntegration.IsRegistered();
    }

    [RelayCommand]
    private void Save()
    {
        var c = _settings.Current;
        c.General.ShowNotifications = ShowNotifications;
        c.General.RunInBackground = RunInBackground;
        c.General.Theme = Theme;
        c.Monitoring.Enabled = MonitoringEnabled;
        c.Monitoring.DebounceSeconds = Math.Clamp(DebounceSeconds, 1, 60);
        c.Monitoring.MinimumConfidence = Math.Clamp(MinimumConfidence, 0.5, 1.0);
        c.AI.Enabled = AiEnabled;
        c.AI.Provider = AiProvider;
        c.AI.Endpoint = AiEndpoint.Trim();
        c.AI.Model = AiModel.Trim();
        c.AI.AllowCloud = AllowCloud;
        c.Privacy.LocalOnly = LocalOnly;
        if (LocalOnly)
        {
            c.AI.AllowCloud = false;
            AllowCloud = false;
        }
        _settings.Save();

        try
        {
            _startup.SetEnabled(StartWithWindows);
            Message = "Settings saved.";
        }
        catch (Exception ex)
        {
            Message = $"Settings saved, but startup change failed: {ex.Message}";
        }
    }

    [RelayCommand]
    private void ToggleContextMenu()
    {
        try
        {
            if (ExplorerIntegration.IsRegistered())
                ExplorerIntegration.Unregister();
            else
                ExplorerIntegration.Register();
            ContextMenuRegistered = ExplorerIntegration.IsRegistered();
        }
        catch (Exception ex)
        {
            Message = $"Context menu change failed: {ex.Message}";
        }
    }

    public void SaveApiKey(string apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            _secrets.DeleteSecret("ai-api-key");
        else
            _secrets.SetSecret("ai-api-key", apiKey.Trim());
    }

    [RelayCommand]
    private void Reset()
    {
        _settings.ResetToDefaults();
        Load();
        Message = "Settings reset to defaults.";
    }
}
