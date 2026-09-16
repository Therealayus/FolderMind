using AIFolderAssistant.Core.Analysis;
using AIFolderAssistant.Core.BusinessRules;
using AIFolderAssistant.Core.Interfaces;
using AIFolderAssistant.Infrastructure.Database;
using AIFolderAssistant.Infrastructure.FileSystem;
using AIFolderAssistant.Infrastructure.Hosting;
using AIFolderAssistant.Infrastructure.Repository;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

// FolderMind engine host: the full pipeline (monitor → analyze → suggest →
// rename → history) without the WinUI shell. Same DI graph, same SQLite DB.
var provider = FolderMindHost.CreateServices(consoleLog: false).BuildServiceProvider();
var log = provider.GetRequiredService<ILoggerFactory>().CreateLogger("Cli");

int Usage()
{
    Console.WriteLine("FolderMind AI — engine host");
    Console.WriteLine("Usage: FolderMind <command> [args]");
    Console.WriteLine("  watch [--debounce N]          Monitor enabled folders until Ctrl+C");
    Console.WriteLine("  analyze <folder>              Analyze once and print the suggestion");
    Console.WriteLine("  folders [list|add <p>|remove <p>|enable <p>|disable <p>]");
    Console.WriteLine("  suggestions [--status Pending|Accepted|Ignored]");
    Console.WriteLine("  rename <folder> <newName>     Rename (explicit, safe, recorded)");
    Console.WriteLine("  undo <folder>                 Undo last rename when safe");
    Console.WriteLine("  history [--clear]");
    Console.WriteLine("  config [show|set <Section.Prop> <value>]");
    return 1;
}

if (args.Length == 0)
    return Usage();

try
{
    return args[0].ToLowerInvariant() switch
    {
        "watch" => await WatchAsync(provider, args[1..]),
        "analyze" => await AnalyzeAsync(provider, args[1..]),
        "folders" => Folders(provider, args[1..]),
        "suggestions" => Suggestions(provider, args[1..]),
        "rename" => await RenameAsync(provider, args[1..]),
        "undo" => await UndoAsync(provider, args[1..]),
        "history" => History(provider, args[1..]),
        "config" => Config(provider, args[1..]),
        _ => Usage()
    };
}
catch (Exception ex)
{
    log.LogError(ex, "Command failed.");
    Console.Error.WriteLine($"Error: {ex.Message}");
    return 2;
}

static async Task<int> WatchAsync(ServiceProvider provider, string[] rest)
{
    var settings = provider.GetRequiredService<ISettingsService>();
    settings.Load();
    var debounce = 3;
    for (var i = 0; i + 1 < rest.Length; i++)
        if (rest[i] == "--debounce" && int.TryParse(rest[i + 1], out var d))
            debounce = Math.Clamp(d, 1, 60);

    var folders = provider.GetRequiredService<IMonitoredFolderRepository>();
    var enabled = folders.GetAll().Where(f => f.IsEnabled).Select(f => f.Path).Distinct().ToList();
    if (enabled.Count == 0)
    {
        Console.WriteLine("No enabled monitored folders. Add one: FolderMind folders add <path>");
        return 1;
    }

    var pipeline = provider.GetRequiredService<IFolderAnalysisPipeline>();
    var analyses = provider.GetRequiredService<IFolderAnalysisRepository>();
    var suggestions = provider.GetRequiredService<ISuggestionRepository>();
    var log = provider.GetRequiredService<ILoggerFactory>().CreateLogger("Watch");
    var threshold = settings.Current.Monitoring.MinimumConfidence;

    using var cts = new CancellationTokenSource();
    Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

    var monitor = provider.GetRequiredService<IFileSystemMonitor>();
    using var watch = new FolderWatchService(monitor,
        provider.GetRequiredService<ILogger<FolderWatchService>>(),
        debounceWindow: TimeSpan.FromSeconds(debounce));
    watch.FolderSettled += (_, folder) =>
    {
        try
        {
            Console.WriteLine($"[settled] {folder} — analyzing…");
            using var actx = new CancellationTokenSource(TimeSpan.FromMinutes(2));
            var result = pipeline.RunPipelineAsync(folder, actx.Token).GetAwaiter().GetResult();
            var match = folders.GetAll().FirstOrDefault(f => folder.StartsWith(f.Path, StringComparison.OrdinalIgnoreCase));
            if (match is not null)
            {
                match.LastScannedAt = DateTime.UtcNow;
                match.UpdatedAt = DateTime.UtcNow;
                folders.Update(match);
            }
            analyses.Add(new FolderAnalysis
            {
                MonitoredFolderId = match?.Id ?? 0,
                FolderPath = folder,
                OriginalName = Path.GetFileName(folder.TrimEnd(Path.DirectorySeparatorChar)) ?? folder,
                SuggestedName = result.SuggestedName,
                Confidence = result.Confidence,
                Reason = result.Reason,
                FileCount = result.FileCount,
                ProcessedFileCount = result.FileCount,
                CreatedAt = DateTime.UtcNow
            });
            if (result.Confidence < threshold)
            {
                Console.WriteLine($"[below threshold {threshold:F2}] {result.SuggestedName} ({result.Confidence:F2})");
                return;
            }
            var name = NameSanitizer.Sanitize(result.SuggestedName);
            var latest = suggestions.GetLatest(folder);
            if (latest?.SuggestedName == name && latest.Status == SuggestionStatus.Pending)
                return;
            suggestions.Add(new Suggestion
            {
                FolderPath = folder,
                OriginalName = Path.GetFileName(folder.TrimEnd(Path.DirectorySeparatorChar)) ?? folder,
                SuggestedName = name,
                Confidence = result.Confidence,
                Reason = result.Reason,
                Status = SuggestionStatus.Pending,
                CreatedAt = DateTime.UtcNow
            });
            Console.WriteLine($"[suggestion] {name} (confidence {result.Confidence:F2}) — {result.Reason}");
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Settled-folder handling failed for {Folder}.", folder);
        }
    };

    watch.Start(enabled);
    Console.WriteLine($"Monitoring {enabled.Count} folder(s), debounce {debounce}s. Press Ctrl+C to stop.");
    try { await Task.Delay(Timeout.Infinite, cts.Token); } catch (OperationCanceledException) { }
    await watch.StopAsync();
    return 0;
}

static async Task<int> AnalyzeAsync(ServiceProvider provider, string[] rest)
{
    if (rest.Length < 1)
    {
        Console.Error.WriteLine("Usage: FolderMind analyze <folder>");
        return 1;
    }
    var pipeline = provider.GetRequiredService<IFolderAnalysisPipeline>();
    using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
    var result = await pipeline.RunPipelineAsync(rest[0], cts.Token);
    Console.WriteLine($"Suggested : {NameSanitizer.Sanitize(result.SuggestedName)}");
    Console.WriteLine($"Confidence: {result.Confidence:F2}");
    Console.WriteLine($"Reason    : {result.Reason}");
    Console.WriteLine($"Files     : {result.FileCount} (local analysis, cloud AI not used)");
    return 0;
}

static int Folders(ServiceProvider provider, string[] rest)
{
    var repo = provider.GetRequiredService<IMonitoredFolderRepository>();
    var watch = provider.GetRequiredService<FolderWatchService>();
    if (rest.Length == 0 || rest[0] == "list")
    {
        foreach (var f in repo.GetAll().OrderBy(f => f.Path))
            Console.WriteLine($"[{(f.IsEnabled ? "on " : "off")}] {f.Path}");
        return 0;
    }
    if (rest.Length < 2)
    {
        Console.Error.WriteLine("Usage: FolderMind folders [list|add|remove|enable|disable] <path>");
        return 1;
    }
    var (ok, error) = PathValidator.ValidateFolderPath(rest[1], mustExist: rest[0] == "add");
    if (!ok && rest[0] == "add")
    {
        Console.Error.WriteLine(error);
        return 1;
    }
    var now = DateTime.UtcNow;
    switch (rest[0])
    {
        case "add":
            if (repo.GetByPath(rest[1]) is not null) { Console.Error.WriteLine("Already monitored."); return 1; }
            repo.Add(new MonitoredFolder { Path = Path.GetFullPath(rest[1]), IsEnabled = true, CreatedAt = now, UpdatedAt = now, LastScannedAt = default });
            Console.WriteLine($"Monitoring {rest[1]}");
            break;
        case "remove":
            repo.Delete(rest[1]);
            try { watch.RemoveFolder(rest[1]); } catch { }
            Console.WriteLine($"Removed {rest[1]}");
            break;
        case "enable":
        case "disable":
            var e = repo.GetByPath(rest[1]);
            if (e is null) { Console.Error.WriteLine("Not monitored."); return 1; }
            e.IsEnabled = rest[0] == "enable";
            e.UpdatedAt = now;
            repo.Update(e);
            Console.WriteLine($"{rest[0]}d {rest[1]}");
            break;
        default:
            Console.Error.WriteLine("Unknown folders action.");
            return 1;
    }
    return 0;
}

static int Suggestions(ServiceProvider provider, string[] rest)
{
    var repo = provider.GetRequiredService<ISuggestionRepository>();
    var status = SuggestionStatus.Pending;
    for (var i = 0; i + 1 < rest.Length; i++)
        if (rest[i] == "--status" && Enum.TryParse<SuggestionStatus>(rest[i + 1], true, out var s))
            status = s;
    var items = repo.GetByStatus(status).OrderByDescending(s => s.CreatedAt);
    var count = 0;
    foreach (var s in items)
    {
        Console.WriteLine($"#{s.Id} {s.OriginalName} → {s.SuggestedName} ({s.Confidence:F2}) [{s.CreatedAt:u}]");
        Console.WriteLine($"    {s.FolderPath}");
        if (!string.IsNullOrEmpty(s.Reason)) Console.WriteLine($"    {s.Reason}");
        count++;
    }
    if (count == 0) Console.WriteLine("No suggestions yet.");
    return 0;
}

static async Task<int> RenameAsync(ServiceProvider provider, string[] rest)
{
    if (rest.Length < 2)
    {
        Console.Error.WriteLine("Usage: FolderMind rename <folder> <newName>");
        return 1;
    }
    var rename = provider.GetRequiredService<IRenameService>();
    var suggestions = provider.GetRequiredService<ISuggestionRepository>();
    var result = await rename.RenameAsync(rest[0], rest[1], CancellationToken.None);
    if (!result.Success)
    {
        Console.Error.WriteLine($"Rename failed: {result.ErrorMessage}");
        return 2;
    }
    var latest = suggestions.GetLatest(rest[0]);
    if (latest is not null) suggestions.UpdateStatus(latest.Id, SuggestionStatus.Accepted, DateTime.UtcNow);
    Console.WriteLine($"Renamed to {result.NewPath}");
    return 0;
}

static async Task<int> UndoAsync(ServiceProvider provider, string[] rest)
{
    if (rest.Length < 1)
    {
        Console.Error.WriteLine("Usage: FolderMind undo <folder>");
        return 1;
    }
    var rename = provider.GetRequiredService<IRenameService>();
    var result = await rename.UndoAsync(rest[0], CancellationToken.None);
    if (!result.Success)
    {
        Console.Error.WriteLine($"Undo failed: {result.ErrorMessage}");
        return 2;
    }
    Console.WriteLine("Rename undone.");
    return 0;
}

static int Config(ServiceProvider provider, string[] rest)
{
    var settings = provider.GetRequiredService<ISettingsService>();
    settings.Load();
    var root = settings.Current;

    if (rest.Length == 0 || rest[0] == "show")
    {
        Console.WriteLine($"Monitoring.Enabled           = {root.Monitoring.Enabled}");
        Console.WriteLine($"Monitoring.DebounceSeconds   = {root.Monitoring.DebounceSeconds}");
        Console.WriteLine($"Monitoring.MinimumConfidence = {root.Monitoring.MinimumConfidence}");
        Console.WriteLine($"General.StartWithWindows     = {root.General.StartWithWindows}");
        Console.WriteLine($"General.ShowNotifications    = {root.General.ShowNotifications}");
        Console.WriteLine($"AI.Enabled                   = {root.AI.Enabled}");
        Console.WriteLine($"AI.AllowCloud                = {root.AI.AllowCloud}");
        Console.WriteLine($"AI.Endpoint                  = {root.AI.Endpoint}");
        Console.WriteLine($"Privacy.LocalOnly            = {root.Privacy.LocalOnly}");
        return 0;
    }

    if (rest[0] == "set" && rest.Length >= 3)
    {
        var (section, prop) = rest[1].Split('.') switch { var p when p.Length == 2 => (p[0], p[1]), _ => ((string?)null, (string?)null) };
        var value = rest[2];
        var applied = (section?.ToLowerInvariant(), prop?.ToLowerInvariant()) switch
        {
            ("monitoring", "enabled") => SetBool(value, v => root.Monitoring.Enabled = v),
            ("monitoring", "debounceseconds") => SetInt(value, v => root.Monitoring.DebounceSeconds = Math.Clamp(v, 1, 60)),
            ("monitoring", "minimumconfidence") => SetDouble(value, v => root.Monitoring.MinimumConfidence = Math.Clamp(v, 0.5, 1.0)),
            ("general", "startwithwindows") => SetBool(value, v => root.General.StartWithWindows = v),
            ("general", "shownotifications") => SetBool(value, v => root.General.ShowNotifications = v),
            _ => false
        };
        if (!applied)
        {
            Console.Error.WriteLine($"Unknown setting: {rest[1]}");
            return 1;
        }
        settings.Save();
        Console.WriteLine($"{rest[1]} = {value}");
        return 0;
    }

    Console.Error.WriteLine("Usage: FolderMind config [show|set <Section.Prop> <value>]");
    return 1;

    static bool SetBool(string s, Action<bool> set)
    {
        if (!bool.TryParse(s, out var v)) return false;
        set(v);
        return true;
    }
    static bool SetInt(string s, Action<int> set)
    {
        if (!int.TryParse(s, out var v)) return false;
        set(v);
        return true;
    }
    static bool SetDouble(string s, Action<double> set)
    {
        if (!double.TryParse(s, out var v)) return false;
        set(v);
        return true;
    }
}

static int History(ServiceProvider provider, string[] rest)
{
    var repo = provider.GetRequiredService<IRenameHistoryRepository>();
    if (rest.Contains("--clear"))
    {
        repo.Clear();
        Console.WriteLine("History cleared.");
        return 0;
    }
    var items = repo.GetAll().OrderByDescending(h => h.CreatedAt).Take(50);
    var count = 0;
    foreach (var h in items)
    {
        Console.WriteLine($"[{(h.Success ? "ok" : "FAIL")}] {h.OriginalName} → {h.NewName} [{h.CreatedAt:u}]");
        count++;
    }
    if (count == 0) Console.WriteLine("No rename history yet.");
    return 0;
}
