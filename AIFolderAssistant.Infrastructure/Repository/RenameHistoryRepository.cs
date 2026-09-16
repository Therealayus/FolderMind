using AIFolderAssistant.Infrastructure.Database;

namespace AIFolderAssistant.Infrastructure.Repository;

public sealed class RenameHistoryRepository : IRenameHistoryRepository
{
    private readonly FolderMindDbContext _db;

    public RenameHistoryRepository(FolderMindDbContext db)
    {
        _db = db;
    }

    public List<RenameHistory> GetAll() => _db.GetRenameHistory();

    public List<RenameHistory> GetByFolderPath(string folderPath) =>
        _db.GetRenameHistory().Where(r => r.FolderPath.Equals(folderPath, StringComparison.OrdinalIgnoreCase)).ToList();

    public RenameHistory? GetLastForFolder(string folderPath)
    {
        var full = folderPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return _db.GetRenameHistory()
            .Where(r =>
                r.FolderPath.Equals(full, StringComparison.OrdinalIgnoreCase) ||
                // A record stores the pre-rename path; undo is queried with the
                // post-rename path (parent + NewName).
                (r.NewName.Length > 0 && ResolvePostRenamePath(r) is string post &&
                 post.Equals(full, StringComparison.OrdinalIgnoreCase)))
            .OrderByDescending(r => r.CreatedAt)
            .FirstOrDefault();
    }

    private static string? ResolvePostRenamePath(RenameHistory record)
    {
        try
        {
            var parent = Path.GetDirectoryName(record.FolderPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            return parent is null ? null : Path.Combine(parent, record.NewName);
        }
        catch
        {
            return null;
        }
    }

    public void Add(RenameHistory history) => _db.InsertRenameHistory(history);

    public void Clear() => _db.ClearRenameHistory();
}
