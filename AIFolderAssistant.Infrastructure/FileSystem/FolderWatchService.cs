using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace AIFolderAssistant.Infrastructure.FileSystem;

/// <summary>
/// Background folder-watch orchestrator implementing the settle-then-analyze pipeline:
/// <para>Folder activity → debounce window → collect files → <see cref="FolderSettled"/> → consumer analyzes.</para>
/// Uses a bounded queue, per-folder debounce timers, retry with backoff, and cooperative cancellation.
/// </summary>
public sealed class FolderWatchService : IDisposable
{
    private readonly IFileSystemMonitor _monitor;
    private readonly ILogger<FolderWatchService> _logger;
    private readonly TimeSpan _debounceWindow;
    private readonly int _maxQueueSize;
    private readonly int _maxRetries;

    private readonly ConcurrentDictionary<string, DateTime> _pendingFolders = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentQueue<string> _queue = new();
    private readonly HashSet<string> _queued = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _queueGate = new();
    private readonly CancellationTokenSource _cts = new();
    private Task? _worker;
    private bool _disposed;

    /// <summary>
    /// Raised when activity in a folder has settled and it is ready for analysis.
    /// </summary>
    public event EventHandler<string>? FolderSettled;

    public bool IsRunning { get; private set; }

    public FolderWatchService(
        IFileSystemMonitor monitor,
        ILogger<FolderWatchService> logger,
        TimeSpan? debounceWindow = null,
        int maxQueueSize = 1000,
        int maxRetries = 3)
    {
        _monitor = monitor;
        _logger = logger;
        _debounceWindow = debounceWindow ?? TimeSpan.FromSeconds(3);
        _maxQueueSize = maxQueueSize;
        _maxRetries = maxRetries;
    }

    public void Start(IEnumerable<string> folders)
    {
        ThrowIfDisposed();
        if (IsRunning)
            return;

        _monitor.FileCreated += OnFileActivity;
        _monitor.FileChanged += OnFileActivity;
        _monitor.FileDeleted += OnFileActivity;
        _monitor.Renamed += OnRenamed;

        foreach (var folder in folders.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                _monitor.StartMonitoring(folder);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Could not monitor {Folder}; continuing with others.", folder);
            }
        }

        IsRunning = true;
        _worker = Task.Run(() => WorkerLoopAsync(_cts.Token));
        _logger.LogInformation("FolderWatchService started (debounce {Debounce}s).", _debounceWindow.TotalSeconds);
    }

    public void AddFolder(string folder)
    {
        ThrowIfDisposed();
        try
        {
            _monitor.StartMonitoring(folder);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not monitor {Folder}.", folder);
            throw;
        }
    }

    public void RemoveFolder(string folder)
    {
        ThrowIfDisposed();
        _monitor.StopMonitoring(folder);
        _pendingFolders.TryRemove(folder, out _);
    }

    public async Task StopAsync()
    {
        if (!IsRunning)
            return;
        IsRunning = false;

        _monitor.FileCreated -= OnFileActivity;
        _monitor.FileChanged -= OnFileActivity;
        _monitor.FileDeleted -= OnFileActivity;
        _monitor.Renamed -= OnRenamed;
        _monitor.StopAll();

        _cts.Cancel();
        if (_worker is not null)
        {
            try { await _worker.ConfigureAwait(false); }
            catch (OperationCanceledException) { /* expected */ }
            catch (Exception ex) { _logger.LogWarning(ex, "Watch worker exited with error."); }
        }
        _logger.LogInformation("FolderWatchService stopped.");
    }

    private void OnFileActivity(object? sender, FileSystemEventArgs e)
    {
        // The unit of analysis is the *target folder*: for activity inside a
        // monitored tree, settle the deepest monitored ancestor; directory
        // creation is detected by watching for new subdirectories.
        var target = ResolveTargetFolder(e);
        if (target is null)
            return;
        MarkActivity(target);
    }

    private void OnRenamed(object? sender, FolderRenamedEventArgs e)
    {
        // Folder rename detection: if a directory itself was renamed, settle
        // the new location so it can be (re)analyzed under its new name.
        try
        {
            if (Directory.Exists(e.NewPath))
            {
                MarkActivity(e.NewPath);
                return;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error inspecting rename target {Path}.", e.NewPath);
        }
        MarkActivity(e.MonitoredFolder);
    }

    private string? ResolveTargetFolder(FileSystemEventArgs e)
    {
        try
        {
            var dir = File.GetAttributes(e.FilePath).HasFlag(FileAttributes.Directory)
                ? e.FilePath
                : Path.GetDirectoryName(e.FilePath);
            if (string.IsNullOrEmpty(dir))
                return e.MonitoredFolder;
            // Clamp to the monitored root so deep trees settle once at top level
            // when the activity is directly under it; nested new folders settle
            // on their own path (directory creation detection).
            if (dir.StartsWith(e.MonitoredFolder, StringComparison.OrdinalIgnoreCase))
                return dir.Equals(e.MonitoredFolder, StringComparison.OrdinalIgnoreCase) ? e.MonitoredFolder : dir;
            return e.MonitoredFolder;
        }
        catch
        {
            // File may have disappeared between event and inspection — still settle parent.
            return e.MonitoredFolder;
        }
    }

    private void MarkActivity(string folder)
    {
        _pendingFolders[folder] = DateTime.UtcNow;
    }

    private async Task WorkerLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromMilliseconds(500), ct).ConfigureAwait(false);
                DrainSettledIntoQueue();
                await ProcessQueueAsync(ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Watch worker iteration failed; continuing.");
            }
        }
    }

    private void DrainSettledIntoQueue()
    {
        var now = DateTime.UtcNow;
        foreach (var kvp in _pendingFolders)
        {
            if (now - kvp.Value < _debounceWindow)
                continue;
            if (!_pendingFolders.TryRemove(kvp.Key, out _))
                continue;
            lock (_queueGate)
            {
                if (_queued.Contains(kvp.Key))
                    continue;
                if (_queue.Count >= _maxQueueSize)
                {
                    _logger.LogWarning("Watch queue full ({Max}); dropping {Folder}.", _maxQueueSize, kvp.Key);
                    continue;
                }
                _queue.Enqueue(kvp.Key);
                _queued.Add(kvp.Key);
            }
        }
    }

    private async Task ProcessQueueAsync(CancellationToken ct)
    {
        while (_queue.TryDequeue(out var folder))
        {
            lock (_queueGate) { _queued.Remove(folder); }
            ct.ThrowIfCancellationRequested();

            var attempt = 0;
            while (true)
            {
                try
                {
                    if (!Directory.Exists(folder))
                    {
                        _logger.LogInformation("Folder {Folder} no longer exists; skipping.", folder);
                        break;
                    }
                    FolderSettled?.Invoke(this, folder);
                    break;
                }
                catch (Exception ex) when (attempt < _maxRetries)
                {
                    attempt++;
                    var backoff = TimeSpan.FromSeconds(Math.Pow(2, attempt));
                    _logger.LogWarning(ex, "Settled-folder dispatch failed for {Folder} (attempt {Attempt}); retrying in {Backoff}s.", folder, attempt, backoff.TotalSeconds);
                    await Task.Delay(backoff, ct).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Settled-folder dispatch failed for {Folder} after {Attempts} attempts.", folder, attempt + 1);
                    break;
                }
            }
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(FolderWatchService));
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        try { StopAsync().GetAwaiter().GetResult(); } catch { /* shutting down */ }
        _cts.Dispose();
    }
}
