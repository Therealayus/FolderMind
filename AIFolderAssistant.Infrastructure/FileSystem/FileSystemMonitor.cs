using Microsoft.Extensions.Logging;

namespace AIFolderAssistant.Infrastructure.FileSystem;

/// <summary>
/// <see cref="FileSystemWatcher"/>-based implementation of <see cref="IFileSystemMonitor"/>.
/// Handles watcher errors without crashing and suppresses rapid duplicate events.
/// Debouncing of "activity settled" semantics lives in <see cref="FolderWatchService"/>.
/// </summary>
public sealed class FileSystemMonitor : IFileSystemMonitor
{
    private readonly ILogger<FileSystemMonitor> _logger;
    private readonly Dictionary<string, FileSystemWatcher> _watchers = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DateTime> _lastEventAt = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _gate = new();
    private readonly TimeSpan _duplicateSuppressionWindow = TimeSpan.FromMilliseconds(500);
    private bool _disposed;

    public FileSystemMonitor(ILogger<FileSystemMonitor> logger)
    {
        _logger = logger;
    }

    public event EventHandler<FileSystemEventArgs>? FileCreated;
    public event EventHandler<FileSystemEventArgs>? FileChanged;
    public event EventHandler<FileSystemEventArgs>? FileDeleted;
    public event EventHandler<FolderRenamedEventArgs>? Renamed;

    public bool IsMonitoring
    {
        get { lock (_gate) { return _watchers.Count > 0; } }
    }

    public void StartMonitoring(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath))
            throw new ArgumentException("Folder path must not be empty.", nameof(folderPath));

        lock (_gate)
        {
            ThrowIfDisposed();
            if (_watchers.ContainsKey(folderPath))
                return;

            try
            {
                if (!Directory.Exists(folderPath))
                    Directory.CreateDirectory(folderPath);

                var watcher = new FileSystemWatcher(folderPath)
                {
                    NotifyFilter = NotifyFilters.FileName
                        | NotifyFilters.DirectoryName
                        | NotifyFilters.LastWrite
                        | NotifyFilters.Size
                        | NotifyFilters.CreationTime,
                    IncludeSubdirectories = true,
                    EnableRaisingEvents = true
                };

                watcher.Created += (s, e) => Raise(FileCreated, e.FullPath, folderPath, WatcherChangeKind.Created);
                watcher.Changed += (s, e) => Raise(FileChanged, e.FullPath, folderPath, WatcherChangeKind.Changed);
                watcher.Deleted += (s, e) => Raise(FileDeleted, e.FullPath, folderPath, WatcherChangeKind.Deleted);
                watcher.Renamed += (s, e) =>
                {
                    try
                    {
                        Renamed?.Invoke(this, new FolderRenamedEventArgs(e.OldFullPath, e.FullPath, folderPath));
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error handling rename event in {Folder}", folderPath);
                    }
                };
                watcher.Error += (s, e) =>
                {
                    _logger.LogError(e.GetException(), "FileSystemWatcher error in {Folder}. Attempting recovery.", folderPath);
                    TryRecover(folderPath);
                };

                _watchers[folderPath] = watcher;
                _logger.LogInformation("Started monitoring {Folder}", folderPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start monitoring {Folder}", folderPath);
                throw;
            }
        }
    }

    public void StopMonitoring(string folderPath)
    {
        lock (_gate)
        {
            if (_watchers.Remove(folderPath, out var watcher))
            {
                watcher.EnableRaisingEvents = false;
                watcher.Dispose();
                _logger.LogInformation("Stopped monitoring {Folder}", folderPath);
            }
        }
    }

    public void StopAll()
    {
        lock (_gate)
        {
            foreach (var watcher in _watchers.Values)
            {
                try
                {
                    watcher.EnableRaisingEvents = false;
                    watcher.Dispose();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error while stopping a watcher.");
                }
            }
            _watchers.Clear();
        }
    }

    public IDisposable SubscribeFileCreated(EventHandler<FileSystemEventArgs> handler) => new Subscription(this, handler, true, false, false, false);
    public IDisposable SubscribeFileChanged(EventHandler<FileSystemEventArgs> handler) => new Subscription(this, handler, false, true, false, false);
    public IDisposable SubscribeFileDeleted(EventHandler<FileSystemEventArgs> handler) => new Subscription(this, handler, false, false, true, false);
    public IDisposable SubscribeRenamed(EventHandler<FolderRenamedEventArgs> handler) => new Subscription(this, handler);

    private void Raise(EventHandler<FileSystemEventArgs>? handler, string fullPath, string folderPath, WatcherChangeKind kind)
    {
        if (string.IsNullOrEmpty(fullPath))
            return;

        // Suppress exact duplicates arriving within the suppression window
        // (FileSystemWatcher is notorious for Created+Changed bursts).
        var dedupeKey = $"{kind}|{fullPath}";
        lock (_gate)
        {
            var now = DateTime.UtcNow;
            if (_lastEventAt.TryGetValue(dedupeKey, out var last) && now - last < _duplicateSuppressionWindow)
                return;
            _lastEventAt[dedupeKey] = now;
        }

        try
        {
            handler?.Invoke(this, new FileSystemEventArgs(fullPath, folderPath, kind));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling {Kind} event for {Path}", kind, fullPath);
        }
    }

    private void TryRecover(string folderPath)
    {
        lock (_gate)
        {
            if (!_watchers.TryGetValue(folderPath, out var watcher))
                return;
            try
            {
                watcher.EnableRaisingEvents = false;
                watcher.EnableRaisingEvents = true;
                _logger.LogInformation("Recovered watcher for {Folder}", folderPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Watcher recovery failed for {Folder}", folderPath);
            }
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(FileSystemMonitor));
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
                return;
            _disposed = true;
        }
        StopAll();
    }

    private sealed class Subscription : IDisposable
    {
        private readonly FileSystemMonitor _owner;
        private readonly Delegate _handler;
        private readonly bool _created, _changed, _deleted;
        private readonly bool _renamed;
        private bool _disposed;

        public Subscription(FileSystemMonitor owner, EventHandler<FileSystemEventArgs> handler, bool created, bool changed, bool deleted, bool renamed)
        {
            _owner = owner;
            _handler = handler;
            _created = created;
            _changed = changed;
            _deleted = deleted;
            _renamed = renamed;
            if (created) owner.FileCreated += handler;
            if (changed) owner.FileChanged += handler;
            if (deleted) owner.FileDeleted += handler;
        }

        public Subscription(FileSystemMonitor owner, EventHandler<FolderRenamedEventArgs> handler)
        {
            _owner = owner;
            _handler = handler;
            _renamed = true;
            owner.Renamed += handler;
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            if (_handler is EventHandler<FileSystemEventArgs> fh)
            {
                if (_created) _owner.FileCreated -= fh;
                if (_changed) _owner.FileChanged -= fh;
                if (_deleted) _owner.FileDeleted -= fh;
            }
            else if (_handler is EventHandler<FolderRenamedEventArgs> rh && _renamed)
            {
                _owner.Renamed -= rh;
            }
        }
    }
}
