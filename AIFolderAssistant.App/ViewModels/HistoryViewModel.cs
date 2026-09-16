using System.Collections.ObjectModel;
using AIFolderAssistant.Core.Interfaces;
using AIFolderAssistant.Core.Models;
using AIFolderAssistant.Infrastructure.Repository;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AIFolderAssistant.App.ViewModels;

public sealed partial class HistoryViewModel : ObservableObject
{
    private readonly IRenameHistoryRepository _history;
    private readonly IRenameService _rename;

    [ObservableProperty] private string _message = string.Empty;

    public ObservableCollection<HistoryEntry> Entries { get; } = new();

    public HistoryViewModel(IRenameHistoryRepository history, IRenameService rename)
    {
        _history = history;
        _rename = rename;
    }

    [RelayCommand]
    private void Refresh()
    {
        Message = string.Empty;
        Entries.Clear();
        foreach (var h in _history.GetAll().OrderByDescending(h => h.CreatedAt).Take(200))
        {
            var currentPath = Path.Combine(Path.GetDirectoryName(h.FolderPath) ?? string.Empty, h.NewName);
            Entries.Add(new HistoryEntry
            {
                FolderPath = h.Success ? currentPath : h.FolderPath,
                OriginalName = h.OriginalName,
                NewName = h.NewName,
                Success = h.Success,
                CreatedAt = h.CreatedAt,
                CanUndo = h.Success && Directory.Exists(currentPath)
            });
        }
        if (Entries.Count == 0)
            Message = "No rename history yet.";
    }

    [RelayCommand]
    private async Task UndoAsync(HistoryEntry? entry)
    {
        if (entry is null || !entry.CanUndo)
            return;
        var result = await _rename.UndoAsync(entry.FolderPath, CancellationToken.None);
        Message = result.Success ? "Rename undone." : $"Undo failed: {result.ErrorMessage}";
        Refresh();
    }

    [RelayCommand]
    private void Clear()
    {
        _history.Clear();
        Refresh();
    }
}
