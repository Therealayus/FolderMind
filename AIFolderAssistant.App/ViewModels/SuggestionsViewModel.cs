using System.Collections.ObjectModel;
using AIFolderAssistant.Core.Interfaces;
using AIFolderAssistant.Core.Models;
using AIFolderAssistant.Infrastructure.Database;
using AIFolderAssistant.Infrastructure.Repository;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AIFolderAssistant.App.ViewModels;

public sealed partial class SuggestionsViewModel : ObservableObject
{
    private readonly ISuggestionRepository _suggestions;
    private readonly IRenameService _rename;

    [ObservableProperty] private string _filter = "Pending";
    [ObservableProperty] private string _message = string.Empty;

    public ObservableCollection<SuggestionInfo> Items { get; } = new();

    public SuggestionsViewModel(ISuggestionRepository suggestions, IRenameService rename)
    {
        _suggestions = suggestions;
        _rename = rename;
    }

    [RelayCommand]
    private void Refresh()
    {
        Message = string.Empty;
        Items.Clear();
        var status = Filter switch
        {
            "Accepted" => SuggestionStatus.Accepted,
            "Ignored" => SuggestionStatus.Ignored,
            _ => SuggestionStatus.Pending
        };
        foreach (var s in _suggestions.GetByStatus(status).OrderByDescending(s => s.CreatedAt))
        {
            Items.Add(new SuggestionInfo
            {
                Id = s.Id,
                FolderPath = s.FolderPath,
                OriginalName = s.OriginalName,
                SuggestedName = s.SuggestedName,
                Confidence = s.Confidence,
                Reason = s.Reason,
                Status = s.Status.ToString(),
                CreatedAt = s.CreatedAt
            });
        }
        if (Items.Count == 0)
            Message = "No suggestions yet.";
    }

    [RelayCommand]
    private async Task RenameAsync(SuggestionInfo? info)
    {
        if (info is null)
            return;
        var result = await _rename.RenameAsync(info.FolderPath, info.SuggestedName, CancellationToken.None);
        if (result.Success)
        {
            _suggestions.UpdateStatus(info.Id, SuggestionStatus.Accepted, DateTime.UtcNow);
            Message = $"Renamed to “{info.SuggestedName}”.";
        }
        else
        {
            Message = $"Rename failed: {result.ErrorMessage}";
        }
        Refresh();
    }

    [RelayCommand]
    private void Ignore(SuggestionInfo? info)
    {
        if (info is null)
            return;
        _suggestions.UpdateStatus(info.Id, SuggestionStatus.Ignored, DateTime.UtcNow);
        Refresh();
    }
}
