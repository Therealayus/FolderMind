using System.Diagnostics;
using System.Text;
using AIFolderAssistant.App.Services;
using AIFolderAssistant.Infrastructure.Repository;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AIFolderAssistant.App.Pages;

public sealed partial class DiagnosticsPage : Page
{
    public DiagnosticsPage()
    {
        InitializeComponent();
        Loaded += (_, _) => Refresh();
    }

    private void Refresh_Click(object sender, RoutedEventArgs e) => Refresh();

    private void Refresh()
    {
        try
        {
            var diag = AppServices.Get<IDiagnosticRepository>();
            var ai = AppServices.Get<IAIUsageRepository>();
            var entries = diag.GetRecent(200);
            SummaryText.Text = $"{entries.Count} recent log entr(ies); {ai.GetAll().Sum(u => u.RequestCount)} AI request(s) recorded.";
            LogList.ItemsSource = entries.Select(d => $"{d.Timestamp:HH:mm:ss} [{d.Level}] {d.Message}").ToList();
        }
        catch (Exception ex)
        {
            SummaryText.Text = $"Could not load diagnostics: {ex.Message}";
        }
    }

    private void Logs_Click(object sender, RoutedEventArgs e)
    {
        var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FolderMind", "logs");
        Directory.CreateDirectory(dir);
        Process.Start(new ProcessStartInfo { FileName = dir, UseShellExecute = true });
    }

    private async void Export_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var picker = new Windows.Storage.Pickers.FileSavePicker();
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWin);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
            picker.FileTypeChoices.Add("Text", new List<string> { ".txt" });
            picker.SuggestedFileName = "foldermind-diagnostics";
            var file = await picker.PickSaveFileAsync();
            if (file is null)
                return;
            var diag = AppServices.Get<IDiagnosticRepository>();
            var sb = new StringBuilder();
            sb.AppendLine($"FolderMind AI diagnostics export — {DateTime.Now}");
            foreach (var d in diag.GetRecent(1000))
                sb.AppendLine($"{d.Timestamp:o} [{d.Level}] {d.Message} {d.Details}");
            await Windows.Storage.FileIO.WriteTextAsync(file, sb.ToString());
        }
        catch (Exception ex)
        {
            SummaryText.Text = $"Export failed: {ex.Message}";
        }
    }
}
