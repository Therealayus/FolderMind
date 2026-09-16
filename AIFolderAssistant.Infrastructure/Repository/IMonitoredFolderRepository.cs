using AIFolderAssistant.Infrastructure.Database;

namespace AIFolderAssistant.Infrastructure.Repository;

public interface IMonitoredFolderRepository
{
    List<MonitoredFolder> GetAll();
    MonitoredFolder? GetByPath(string path);
    void Add(MonitoredFolder folder);
    void Update(MonitoredFolder folder);
    void Delete(string path);
}
