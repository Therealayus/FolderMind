namespace AIFolderAssistant.Tray;

/// <summary>
/// FolderMind AI tray entry point. WinExe (no console window ever flashes).
/// Single main instance lives in the tray; extra launches either analyze
/// one folder (--analyze) or exit quietly when the main instance runs.
/// </summary>
internal static class Program
{
    private const string MutexName = @"Local\FolderMindAI-SingleInstance";

    [STAThread]
    private static int Main(string[] args)
    {
        ApplicationConfiguration.Initialize();

        var analyzeIdx = Array.FindIndex(args, a => a.Equals("--analyze", StringComparison.OrdinalIgnoreCase));
        var analyzePath = analyzeIdx >= 0 && analyzeIdx + 1 < args.Length ? args[analyzeIdx + 1] : null;

        using var mutex = new Mutex(initiallyOwned: true, MutexName, out var isMain);
        if (!isMain && analyzePath is null)
        {
            // Main instance already runs; plain relaunch would only duplicate the icon.
            return 0;
        }

        using var context = new TrayApplicationContext(!isMain, analyzePath);
        Application.Run(context);
        return 0;
    }
}
