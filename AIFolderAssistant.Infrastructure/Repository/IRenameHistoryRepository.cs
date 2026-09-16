using AIFolderAssistant.Infrastructure.Database;

namespace AIFolderAssistant.Infrastructure.Repository;

public interface IRenameHistoryRepository
{
    List<RenameHistory> GetAll();
    List<RenameHistory> GetByFolderPath(string folderPath);
    RenameHistory? GetLastForFolder(string folderPath);
    void Add(RenameHistory history);
    void Clear();
}
