using AIFolderAssistant.App.Services;
using AIFolderAssistant.App.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AIFolderAssistant.App.Pages;

public sealed partial class HomePage : Page
{
    public HomeViewModel ViewModel { get; }

    public HomePage()
    {
        InitializeComponent();
        ViewModel = AppServices.Get<HomeViewModel>();
        Loaded += (_, _) => ViewModel.RefreshCommand.Execute(null);
    }

    private void Refresh_Click(object sender, RoutedEventArgs e) => ViewModel.RefreshCommand.Execute(null);

    private async void Analyze_Click(object sender, RoutedEventArgs e)
    {
        var picker = new Windows.Storage.Pickers.FolderPicker();
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWin);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
        picker.FileTypeFilter.Add("*");
        var folder = await picker.PickSingleFolderAsync();
        if (folder is not null)
        {
            var pipeline = AppServices.Get<Core.Analysis.IFolderAnalysisPipeline>();
            using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromMinutes(2));
            var result = await pipeline.RunPipelineAsync(folder.Path, cts.Token);
            var notifier = AppServices.Get<Core.Interfaces.INotificationService>();
            notifier.NotifySuggestion(folder.Path, folder.Name,
                Core.BusinessRules.NameSanitizer.Sanitize(result.SuggestedName), result.Confidence, result.Reason);
        }
    }
}
