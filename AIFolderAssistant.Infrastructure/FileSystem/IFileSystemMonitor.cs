namespace AIFolderAssistant.Infrastructure.FileSystem;

/// <summary>
/// Abstraction over <see cref="FileSystemWatcher"/> so monitoring can be
/// tested, replaced, or extended without touching consumers.
/// </summary>
public interface IFileSystemMonitor : IDisposable
{
    event EventHandler<FileSystemEventArgs>? FileCreated;
    event EventHandler<FileSystemEventArgs>? FileChanged;
    event EventHandler<FileSystemEventArgs>? FileDeleted;
    event EventHandler<FolderRenamedEventArgs>? Renamed;

    bool IsMonitoring { get; }

    void StartMonitoring(string folderPath);
    void StopMonitoring(string folderPath);
    void StopAll();

    IDisposable SubscribeFileCreated(EventHandler<FileSystemEventArgs> handler);
    IDisposable SubscribeFileChanged(EventHandler<FileSystemEventArgs> handler);
    IDisposable SubscribeFileDeleted(EventHandler<FileSystemEventArgs> handler);
    IDisposable SubscribeRenamed(EventHandler<FolderRenamedEventArgs> handler);
}
