using AIFolderAssistant.App.Services;
using AIFolderAssistant.App.ViewModels;
using AIFolderAssistant.Core.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AIFolderAssistant.App.Pages;

public sealed partial class SuggestionsPage : Page
{
    public SuggestionsViewModel ViewModel { get; }

    public SuggestionsPage()
    {
        InitializeComponent();
        ViewModel = AppServices.Get<SuggestionsViewModel>();
        Loaded += (_, _) => ViewModel.RefreshCommand.Execute(null);
    }

    private void Refresh_Click(object sender, RoutedEventArgs e) => ViewModel.RefreshCommand.Execute(null);

    private void Filter_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (FilterBox.SelectedItem is ComboBoxItem item && item.Content is string s)
        {
            ViewModel.Filter = s;
            ViewModel.RefreshCommand.Execute(null);
        }
    }

    private void Rename_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is SuggestionInfo info)
            _ = ViewModel.RenameCommand.ExecuteAsync(info);
    }

    private void Ignore_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is SuggestionInfo info)
            ViewModel.IgnoreCommand.Execute(info);
    }
}
