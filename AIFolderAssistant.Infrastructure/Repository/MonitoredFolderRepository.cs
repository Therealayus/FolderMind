using AIFolderAssistant.Infrastructure.Database;

namespace AIFolderAssistant.Infrastructure.Repository;

public sealed class MonitoredFolderRepository : IMonitoredFolderRepository
{
    private readonly FolderMindDbContext _db;

    public MonitoredFolderRepository(FolderMindDbContext db)
    {
        _db = db;
    }

    public List<MonitoredFolder> GetAll() => _db.GetMonitoredFolders();

    public MonitoredFolder? GetByPath(string path) =>
        _db.GetMonitoredFolders().FirstOrDefault(f => f.Path.Equals(path, StringComparison.OrdinalIgnoreCase));

    public void Add(MonitoredFolder folder) => _db.InsertMonitoredFolder(folder);

    public void Update(MonitoredFolder folder) => _db.UpdateMonitoredFolder(folder);

    public void Delete(string path) => _db.DeleteMonitoredFolder(path);
}
