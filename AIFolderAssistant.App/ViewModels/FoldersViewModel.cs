using System.Collections.ObjectModel;
using AIFolderAssistant.Core.BusinessRules;
using AIFolderAssistant.Core.Models;
using AIFolderAssistant.Infrastructure.Database;
using AIFolderAssistant.Infrastructure.Repository;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AIFolderAssistant.App.ViewModels;

public sealed partial class FoldersViewModel : ObservableObject
{
    private readonly IMonitoredFolderRepository _repository;

    [ObservableProperty] private string _errorMessage = string.Empty;

    public ObservableCollection<MonitoredFolderInfo> Folders { get; } = new();

    public FoldersViewModel(IMonitoredFolderRepository repository)
    {
        _repository = repository;
    }

    [RelayCommand]
    private void Refresh()
    {
        ErrorMessage = string.Empty;
        Folders.Clear();
        foreach (var f in _repository.GetAll().OrderBy(f => f.Path))
        {
            Folders.Add(new MonitoredFolderInfo
            {
                Path = f.Path,
                IsEnabled = f.IsEnabled,
                LastScannedAt = f.LastScannedAt
            });
        }
    }

    public void AddFolder(string path)
    {
        var (ok, error) = PathValidator.ValidateFolderPath(path, mustExist: true);
        if (!ok)
        {
            ErrorMessage = error ?? "Invalid folder.";
            return;
        }
        if (_repository.GetByPath(path) is not null)
        {
            ErrorMessage = "This folder is already monitored.";
            return;
        }
        var now = DateTime.UtcNow;
        _repository.Add(new MonitoredFolder
        {
            Path = Path.GetFullPath(path),
            IsEnabled = true,
            CreatedAt = now,
            UpdatedAt = now,
            LastScannedAt = default
        });
        Refresh();
    }

    [RelayCommand]
    private void Toggle(MonitoredFolderInfo? info)
    {
        if (info is null)
            return;
        var entity = _repository.GetByPath(info.Path);
        if (entity is null)
            return;
        entity.IsEnabled = !entity.IsEnabled;
        entity.UpdatedAt = DateTime.UtcNow;
        _repository.Update(entity);
        Refresh();
    }

    [RelayCommand]
    private void Remove(MonitoredFolderInfo? info)
    {
        if (info is null)
            return;
        _repository.Delete(info.Path);
        Refresh();
    }
}
