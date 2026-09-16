using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using AIFolderAssistant.Core.Analysis;
using AIFolderAssistant.Core.BusinessRules;
using AIFolderAssistant.Core.Interfaces;
using AIFolderAssistant.Infrastructure.Database;
using AIFolderAssistant.Infrastructure.FileSystem;
using AIFolderAssistant.Infrastructure.Hosting;
using AIFolderAssistant.Infrastructure.Repository;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AIFolderAssistant.Tray;

/// <summary>
/// The background application: tray icon + menu, folder watching,
/// settle → analyze → suggest → balloon pipeline, and one-shot --analyze.
/// Closing the suggestions window never exits; only Exit quits.
/// </summary>
internal sealed class TrayApplicationContext : ApplicationContext
{
    private readonly ServiceProvider _services;
    private readonly ILogger<TrayApplicationContext> _log;
    private readonly Form _marshal;
    private readonly NotifyIcon _tray;
    private readonly ToolStripMenuItem _statusItem;
    private readonly ToolStripMenuItem _toggleItem;
    private readonly ToolStripMenuItem _startupItem;
    private readonly Bitmap _iconActive;
    private readonly Bitmap _iconPaused;
    private readonly IntPtr _hIconActive;
    private readonly IntPtr _hIconPaused;
    private bool _active;
    private readonly FolderWatchService _watch;
    private SuggestionsForm? _suggestionsForm;
    private bool _exitRequested;

    internal TrayApplicationContext(bool oneShot, string? analyzePath)
    {
        _services = FolderMindHost.CreateServices(consoleLog: false).BuildServiceProvider();
        _log = _services.GetRequiredService<ILogger<TrayApplicationContext>>();

        // Hidden marshal window: guarantees a UI-thread pump for cross-thread calls.
        // NOTE: Control.CreateControl() is a no-op for invisible Forms — the
        // Handle property getter is what actually forces handle creation.
        _marshal = new Form { ShowInTaskbar = false, WindowState = FormWindowState.Minimized };
        _ = _marshal.Handle;

        (_iconActive, _hIconActive) = MakeIcon(Color.FromArgb(0x4F, 0x6B, 0xED));
        (_iconPaused, _hIconPaused) = MakeIcon(Color.FromArgb(0x77, 0x77, 0x77));

        _tray = new NotifyIcon
        {
            Text = "FolderMind AI",
            Visible = true
        };
        _tray.MouseClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left)
                _marshal.BeginInvoke(ShowSuggestions);
        };
        _tray.BalloonTipClicked += (_, _) => _marshal.BeginInvoke(ShowSuggestions);

        var menu = new ContextMenuStrip();
        menu.Items.Add(new ToolStripMenuItem("FolderMind AI") { Enabled = false });
        menu.Items.Add(new ToolStripSeparator());
        _statusItem = new ToolStripMenuItem("○ Paused") { Enabled = false };
        menu.Items.Add(_statusItem);
        _toggleItem = new ToolStripMenuItem("Resume Monitoring", null, (_, _) => _marshal.BeginInvoke(new Action(async () => await SetPausedAsync(_active))));
        menu.Items.Add(_toggleItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem("Open FolderMind", null, (_, _) => _marshal.BeginInvoke(ShowSuggestions)));
        menu.Items.Add(new ToolStripMenuItem("Analyze Folder…", null, (_, _) => _marshal.BeginInvoke(AnalyzeFolderInteractiveAsync)));
        _startupItem = new ToolStripMenuItem("Start with Windows", null, (_, _) => _marshal.BeginInvoke(ToggleStartup))
        {
            CheckOnClick = false
        };
        menu.Items.Add(_startupItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem("Exit", null, (_, _) => _marshal.BeginInvoke(ExitApp)));
        _tray.ContextMenuStrip = menu;

        var settings = _services.GetRequiredService<ISettingsService>();
        settings.Load();
        _watch = _services.GetRequiredService<FolderWatchService>();
        _watch.FolderSettled += OnFolderSettled;

        if (oneShot && analyzePath is not null)
        {
            SetTrayState(active: false, "FolderMind AI — analyzing…");
            _ = AnalyzeAndBalloonAsync(analyzePath, stayAlive: TimeSpan.FromSeconds(8));
            return;
        }

        var enabled = settings.Current.Monitoring.Enabled
            ? _services.GetRequiredService<IMonitoredFolderRepository>().GetAll()
                .Where(f => f.IsEnabled).Select(f => f.Path).Distinct().ToList()
            : new List<string>();
        _watch.Start(enabled);
        SetTrayState(settings.Current.Monitoring.Enabled && enabled.Count > 0, null);
        RefreshStartupCheck();
    }

    // ---------- tray state ----------

    private void SetTrayState(bool active, string? tooltip)
    {
        _active = active;
        _tray.Icon = Icon.FromHandle(active ? _hIconActive : _hIconPaused);
        _statusItem.Text = active ? "● Active" : "○ Paused";
        _toggleItem.Text = active ? "Pause Monitoring" : "Resume Monitoring";
        if (tooltip is not null)
            _tray.Text = tooltip.Length > 63 ? tooltip.Substring(0, 63) : tooltip;
        else
            _tray.Text = "FolderMind AI";
    }

    private void RefreshStartupCheck()
    {
        try
        {
            _startupItem.Checked = _services.GetRequiredService<IStartupService>().IsEnabled();
        }
        catch { _startupItem.Checked = false; }
    }

    private void ToggleStartup()
    {
        try
        {
            var svc = _services.GetRequiredService<IStartupService>();
            svc.SetEnabled(!svc.IsEnabled());
            RefreshStartupCheck();
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Startup toggle failed.");
            _tray.ShowBalloonTip(3000, "FolderMind AI", $"Could not change startup setting: {ex.Message}", ToolTipIcon.Warning);
        }
    }

    private async Task SetPausedAsync(bool paused)
    {
        try
        {
            var settings = _services.GetRequiredService<ISettingsService>();
            settings.Current.Monitoring.Enabled = !paused;
            settings.Save();
            if (paused)
            {
                await _watch.StopAsync();
                SetTrayState(false, null);
            }
            else
            {
                var folders = _services.GetRequiredService<IMonitoredFolderRepository>().GetAll()
                    .Where(f => f.IsEnabled).Select(f => f.Path).Distinct().ToList();
                _watch.Start(folders);
                SetTrayState(true, null);
            }
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Pause/resume failed.");
        }
    }

    // ---------- analysis pipeline ----------

    private void OnFolderSettled(object? sender, string folder)
    {
        // Arrives on a watcher thread — marshal everything to the UI thread.
        _marshal.BeginInvoke(() => _ = HandleSettledAsync(folder));
    }

    private async Task HandleSettledAsync(string folder)
    {
        var settings = _services.GetRequiredService<ISettingsService>();
        if (!settings.Current.Monitoring.Enabled)
            return;
        var result = await RunAnalysisAsync(folder);
        if (result is null)
            return;

        var folders = _services.GetRequiredService<IMonitoredFolderRepository>();
        var match = folders.GetAll()
            .Where(f => folder.StartsWith(f.Path, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(f => f.Path.Length)
            .FirstOrDefault();
        if (match is not null)
        {
            match.LastScannedAt = DateTime.UtcNow;
            match.UpdatedAt = DateTime.UtcNow;
            folders.Update(match);
        }
        PersistAnalysis(folder, result, match?.Id ?? 0);

        if (result.Confidence < settings.Current.Monitoring.MinimumConfidence)
            return;

        var name = NameSanitizer.Sanitize(result.SuggestedName);
        var suggestions = _services.GetRequiredService<ISuggestionRepository>();
        var latest = suggestions.GetLatest(folder);
        if (latest?.SuggestedName == name && latest.Status == SuggestionStatus.Pending)
            return;

        var original = Leaf(folder);
        suggestions.Add(new Suggestion
        {
            FolderPath = folder,
            OriginalName = original,
            SuggestedName = name,
            Confidence = result.Confidence,
            Reason = result.Reason,
            Status = SuggestionStatus.Pending,
            CreatedAt = DateTime.UtcNow
        });
        ShowSuggestionBalloon(original, name, result.Confidence);
    }

    private async Task AnalyzeAndBalloonAsync(string folder, TimeSpan stayAlive)
    {
        try
        {
            var result = await RunAnalysisAsync(folder);
            if (result is null)
            {
                _tray.ShowBalloonTip(4000, "FolderMind AI", "Could not analyze that folder.", ToolTipIcon.Warning);
            }
            else
            {
                var name = NameSanitizer.Sanitize(result.SuggestedName);
                _services.GetRequiredService<ISuggestionRepository>().Add(new Suggestion
                {
                    FolderPath = folder,
                    OriginalName = Leaf(folder),
                    SuggestedName = name,
                    Confidence = result.Confidence,
                    Reason = result.Reason,
                    Status = SuggestionStatus.Pending,
                    CreatedAt = DateTime.UtcNow
                });
                ShowSuggestionBalloon(Leaf(folder), name, result.Confidence);
            }
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "One-shot analysis failed for {Folder}.", folder);
        }
        await Task.Delay(stayAlive);
        ExitApp();
    }

    private async void AnalyzeFolderInteractiveAsync()
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Choose a folder for FolderMind to analyze",
            UseDescriptionForTitle = true
        };
        if (dialog.ShowDialog() != DialogResult.OK)
            return;
        var result = await RunAnalysisAsync(dialog.SelectedPath);
        if (result is null)
        {
            _tray.ShowBalloonTip(4000, "FolderMind AI", "Could not analyze that folder.", ToolTipIcon.Warning);
            return;
        }
        ShowSuggestionBalloon(Leaf(dialog.SelectedPath), NameSanitizer.Sanitize(result.SuggestedName), result.Confidence);
    }

    private async Task<FolderAnalysisResult?> RunAnalysisAsync(string folder)
    {
        try
        {
            var pipeline = _services.GetRequiredService<IFolderAnalysisPipeline>();
            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
            return await pipeline.RunPipelineAsync(folder, cts.Token);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Analysis failed for {Folder}.", folder);
            return null;
        }
    }

    private void PersistAnalysis(string folder, FolderAnalysisResult result, int monitoredFolderId)
    {
        try
        {
            _services.GetRequiredService<IFolderAnalysisRepository>().Add(new FolderAnalysis
            {
                MonitoredFolderId = monitoredFolderId,
                FolderPath = folder,
                OriginalName = Leaf(folder),
                SuggestedName = result.SuggestedName,
                Confidence = result.Confidence,
                Reason = result.Reason,
                FileCount = result.FileCount,
                ProcessedFileCount = result.FileCount,
                CreatedAt = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Could not persist analysis for {Folder}.", folder);
        }
    }

    private void ShowSuggestionBalloon(string original, string suggested, double confidence)
    {
        var band = confidence >= 0.9 ? "High" : confidence >= 0.8 ? "Medium" : "Low";
        try
        {
            _tray.ShowBalloonTip(8000, $"Better name for “{original}”", $"{suggested} ({band} confidence) — click to review", ToolTipIcon.Info);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Balloon failed.");
        }
    }

    // ---------- windows ----------

    private void ShowSuggestions()
    {
        if (_suggestionsForm is { IsDisposed: false })
        {
            _suggestionsForm.Activate();
            return;
        }
        _suggestionsForm = new SuggestionsForm(_services);
        _suggestionsForm.Show();
    }

    private async void ExitApp()
    {
        if (_exitRequested)
            return;
        _exitRequested = true;
        try { await _watch.StopAsync(); } catch { }
        _tray.Visible = false;
        _tray.Dispose();
        _suggestionsForm?.Close();
        _marshal.Dispose();
        DestroyIcon(_hIconActive);
        DestroyIcon(_hIconPaused);
        _iconActive.Dispose();
        _iconPaused.Dispose();
        _services.Dispose();
        Application.Exit();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !_exitRequested)
        {
            try { _watch.StopAsync().GetAwaiter().GetResult(); } catch { }
            _tray.Visible = false;
            _tray.Dispose();
        }
        base.Dispose(disposing);
    }

    // ---------- icon art (drawn in code — no binary assets needed) ----------

    private static (Bitmap bitmap, IntPtr hIcon) MakeIcon(Color accent)
    {
        const int size = 32;
        var bmp = new Bitmap(size, size);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);
            using var brush = new SolidBrush(accent);
            using var path = RoundedRect(1, 1, size - 2, size - 2, 8);
            g.FillPath(brush, path);
            using var font = new Font(FontFamily.GenericSansSerif, 13, FontStyle.Bold, GraphicsUnit.Pixel);
            var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            g.DrawString("F", font, Brushes.White, new RectangleF(0, 1, size, size), sf);
        }
        return (bmp, bmp.GetHicon());
    }

    private static GraphicsPath RoundedRect(int x, int y, int w, int h, int r)
    {
        var path = new GraphicsPath();
        path.AddArc(x, y, r * 2, r * 2, 180, 90);
        path.AddArc(x + w - r * 2, y, r * 2, r * 2, 270, 90);
        path.AddArc(x + w - r * 2, y + h - r * 2, r * 2, r * 2, 0, 90);
        path.AddArc(x, y + h - r * 2, r * 2, r * 2, 90, 90);
        path.CloseFigure();
        return path;
    }

    private static string Leaf(string folder) =>
        Path.GetFileName(folder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)) is string { Length: > 0 } leaf
            ? leaf
            : folder;

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr hIcon);
}
