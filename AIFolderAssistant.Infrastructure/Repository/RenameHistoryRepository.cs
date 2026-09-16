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

    public RenameHistory? GetLastForFolder(string folderPath) =>
        _db.GetRenameHistory()
            .Where(r => r.FolderPath.Equals(folderPath, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(r => r.CreatedAt)
            .FirstOrDefault();

    public void Add(RenameHistory history) => _db.InsertRenameHistory(history);

    public void Clear() => _db.ClearRenameHistory();
}
