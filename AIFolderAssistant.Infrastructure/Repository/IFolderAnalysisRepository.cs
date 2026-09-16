using AIFolderAssistant.Infrastructure.Database;

namespace AIFolderAssistant.Infrastructure.Repository;

public interface IFolderAnalysisRepository
{
    List<FolderAnalysis> GetByFolderPath(string folderPath);
    FolderAnalysis? GetLatest(string folderPath);
    void Add(FolderAnalysis analysis);
}
