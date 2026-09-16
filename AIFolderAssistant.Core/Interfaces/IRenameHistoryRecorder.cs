namespace AIFolderAssistant.Core.Interfaces;

/// <summary>
/// Persistence abstraction for rename history so <see cref="Services.RenameService"/>
/// stays independent of the SQLite layer.
/// </summary>
public interface IRenameHistoryRecorder
{
    void Record(string folderPath, string originalName, string newName, bool success, string? errorMessage);
    RenameHistoryEntry? GetLastForFolder(string folderPath);
}

public sealed class RenameHistoryEntry
{
    public string FolderPath { get; set; } = string.Empty;
    public string OriginalName { get; set; } = string.Empty;
    public string NewName { get; set; } = string.Empty;
    public bool Success { get; set; }
    public DateTime CreatedAt { get; set; }
}
