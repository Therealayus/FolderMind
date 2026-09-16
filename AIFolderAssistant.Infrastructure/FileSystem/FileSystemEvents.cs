namespace AIFolderAssistant.Infrastructure.FileSystem;

/// <summary>
/// Type of file-system change observed in a monitored folder.
/// </summary>
public enum WatcherChangeKind
{
    Created,
    Changed,
    Deleted,
    Renamed
}

/// <summary>
/// Event data for file create/change/delete notifications.
/// </summary>
public sealed class FileSystemEventArgs : EventArgs
{
    public string FilePath { get; }
    public string MonitoredFolder { get; }
    public WatcherChangeKind ChangeKind { get; }

    public FileSystemEventArgs(string filePath, string monitoredFolder, WatcherChangeKind changeKind)
    {
        FilePath = filePath;
        MonitoredFolder = monitoredFolder;
        ChangeKind = changeKind;
    }
}

/// <summary>
/// Event data for rename notifications.
/// </summary>
public sealed class FolderRenamedEventArgs : EventArgs
{
    public string OldPath { get; }
    public string NewPath { get; }
    public string MonitoredFolder { get; }

    public FolderRenamedEventArgs(string oldPath, string newPath, string monitoredFolder)
    {
        OldPath = oldPath;
        NewPath = newPath;
        MonitoredFolder = monitoredFolder;
    }
}
