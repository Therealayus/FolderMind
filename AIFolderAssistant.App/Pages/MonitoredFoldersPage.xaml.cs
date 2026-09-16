using AIFolderAssistant.App.Services;
using AIFolderAssistant.App.ViewModels;
using AIFolderAssistant.Core.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AIFolderAssistant.App.Pages;

public sealed partial class MonitoredFoldersPage : Page
{
    public FoldersViewModel ViewModel { get; }

    public MonitoredFoldersPage()
    {
        InitializeComponent();
        ViewModel = AppServices.Get<FoldersViewModel>();
        Loaded += (_, _) => ViewModel.RefreshCommand.Execute(null);
    }

    private void Refresh_Click(object sender, RoutedEventArgs e) => ViewModel.RefreshCommand.Execute(null);

    private async void Add_Click(object sender, RoutedEventArgs e)
    {
        var picker = new Windows.Storage.Pickers.FolderPicker();
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWin);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
        picker.FileTypeFilter.Add("*");
        var folder = await picker.PickSingleFolderAsync();
        if (folder is not null)
        {
            ViewModel.AddFolder(folder.Path);
            try
            {
                AppServices.Get<Infrastructure.FileSystem.FolderWatchService>().AddFolder(folder.Path);
                AppServices.Get<Core.Interfaces.ITrayController>()?.SetMonitoringActive(true);
            }
            catch
            {
                // Repository already updated; watcher will pick it up on restart.
            }
        }
    }

    private void Toggle_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is MonitoredFolderInfo info)
            ViewModel.ToggleCommand.Execute(info);
    }

    private void Remove_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is MonitoredFolderInfo info)
        {
            ViewModel.RemoveCommand.Execute(info);
            try { AppServices.Get<Infrastructure.FileSystem.FolderWatchService>().RemoveFolder(info.Path); }
            catch { /* already removed from repository */ }
        }
    }
}
