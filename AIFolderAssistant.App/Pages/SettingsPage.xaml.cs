using AIFolderAssistant.App.Services;
using AIFolderAssistant.App.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AIFolderAssistant.App.Pages;

public sealed partial class SettingsPage : Page
{
    public SettingsViewModel ViewModel { get; }

    public SettingsPage()
    {
        InitializeComponent();
        ViewModel = AppServices.Get<SettingsViewModel>();
        Loaded += (_, _) => ViewModel.LoadCommand.Execute(null);
    }

    private void Save_Click(object sender, RoutedEventArgs e) => ViewModel.SaveCommand.Execute(null);

    private void Reset_Click(object sender, RoutedEventArgs e) => ViewModel.ResetCommand.Execute(null);

    private void Menu_Click(object sender, RoutedEventArgs e) => ViewModel.ToggleContextMenuCommand.Execute(null);

    private void SaveKey_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.SaveApiKey(ApiKeyBox.Password);
        ApiKeyBox.Password = string.Empty;
    }
}
