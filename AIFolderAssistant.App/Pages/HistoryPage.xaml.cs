using AIFolderAssistant.App.Services;
using AIFolderAssistant.App.ViewModels;
using AIFolderAssistant.Core.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AIFolderAssistant.App.Pages;

public sealed partial class HistoryPage : Page
{
    public HistoryViewModel ViewModel { get; }

    public HistoryPage()
    {
        InitializeComponent();
        ViewModel = AppServices.Get<HistoryViewModel>();
        Loaded += (_, _) => ViewModel.RefreshCommand.Execute(null);
    }

    private void Refresh_Click(object sender, RoutedEventArgs e) => ViewModel.RefreshCommand.Execute(null);

    private void Clear_Click(object sender, RoutedEventArgs e) => ViewModel.ClearCommand.Execute(null);

    private void Undo_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is HistoryEntry entry)
            _ = ViewModel.UndoCommand.ExecuteAsync(entry);
    }
}
