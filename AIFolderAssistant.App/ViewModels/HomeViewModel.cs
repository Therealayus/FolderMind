using System.Collections.ObjectModel;
using AIFolderAssistant.Infrastructure.Repository;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AIFolderAssistant.App.ViewModels;

public sealed partial class HomeViewModel : ObservableObject
{
    private readonly IMonitoredFolderRepository _folders;
    private readonly ISuggestionRepository _suggestions;

    [ObservableProperty] private int _monitoredCount;
    [ObservableProperty] private int _pendingCount;
    [ObservableProperty] private string _statusLine = "Not enough data yet";

    public ObservableCollection<string> Recent { get; } = new();

    public HomeViewModel(IMonitoredFolderRepository folders, ISuggestionRepository suggestions)
    {
        _folders = folders;
        _suggestions = suggestions;
    }

    [RelayCommand]
    private void Refresh()
    {
        try
        {
            var all = _folders.GetAll();
            MonitoredCount = all.Count(f => f.IsEnabled);
            var pending = _suggestions.GetByStatus(Infrastructure.Database.SuggestionStatus.Pending);
            PendingCount = pending.Count;
            Recent.Clear();
            foreach (var s in pending.OrderByDescending(s => s.CreatedAt).Take(5))
                Recent.Add($"{s.OriginalName} → {s.SuggestedName}");
            StatusLine = pending.Count == 0 && all.Count == 0
                ? "No suggestions yet — add a folder to monitor to get started."
                : $"{PendingCount} pending suggestion(s) across {MonitoredCount} monitored folder(s).";
        }
        catch
        {
            StatusLine = "Could not load dashboard data.";
        }
    }
}
