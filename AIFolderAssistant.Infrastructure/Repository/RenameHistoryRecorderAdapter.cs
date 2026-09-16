using AIFolderAssistant.Core.Interfaces;
using AIFolderAssistant.Infrastructure.Database;

namespace AIFolderAssistant.Infrastructure.Repository;

/// <summary>
/// Bridges <see cref="IRenameHistoryRecorder"/> (Core) to the SQLite-backed
/// <see cref="IRenameHistoryRepository"/>.
/// </summary>
public sealed class RenameHistoryRecorderAdapter : IRenameHistoryRecorder
{
    private readonly IRenameHistoryRepository _repository;

    public RenameHistoryRecorderAdapter(IRenameHistoryRepository repository)
    {
        _repository = repository;
    }

    public void Record(string folderPath, string originalName, string newName, bool success, string? errorMessage) =>
        _repository.Add(new RenameHistory
        {
            FolderPath = folderPath,
            OriginalName = originalName,
            NewName = newName,
            Success = success,
            ErrorMessage = errorMessage,
            CreatedAt = DateTime.UtcNow
        });

    public RenameHistoryEntry? GetLastForFolder(string folderPath)
    {
        var last = _repository.GetLastForFolder(folderPath);
        return last is null ? null : new RenameHistoryEntry
        {
            FolderPath = last.FolderPath,
            OriginalName = last.OriginalName,
            NewName = last.NewName,
            Success = last.Success,
            CreatedAt = last.CreatedAt
        };
    }
}
