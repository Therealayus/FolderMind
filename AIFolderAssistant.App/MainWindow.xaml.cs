using AIFolderAssistant.App.Pages;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AIFolderAssistant.App;

/// <summary>
/// Main window: NavigationView shell. Closing hides to tray — only Exit quits.
/// </summary>
public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Title = "FolderMind AI";

        // Close button hides to tray instead of terminating the background process.
        AppWindow.Closing += (s, e) =>
        {
            if (!App.ExitRequested)
            {
                e.Cancel = true;
                this.Hide();
            }
        };

        Navigate("home");
    }

    private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is NavigationViewItem item && item.Tag is string tag)
            Navigate(tag);
    }

    public void Navigate(string tag)
    {
        Type page = tag switch
        {
            "suggestions" => typeof(SuggestionsPage),
            "folders" => typeof(MonitoredFoldersPage),
            "history" => typeof(HistoryPage),
            "settings" => typeof(SettingsPage),
            "diagnostics" => typeof(DiagnosticsPage),
            "about" => typeof(AboutPage),
            _ => typeof(HomePage),
        };
        ContentFrame.Navigate(page);
        HeaderText.Text = tag switch
        {
            "suggestions" => "Suggestions",
            "folders" => "Monitored Folders",
            "history" => "History",
            "settings" => "Settings",
            "diagnostics" => "Diagnostics",
            "about" => "About",
            _ => "FolderMind AI",
        };
        foreach (var m in NavView.MenuItems.OfType<NavigationViewItem>())
            m.IsSelected = (m.Tag as string) == tag;
    }
}
