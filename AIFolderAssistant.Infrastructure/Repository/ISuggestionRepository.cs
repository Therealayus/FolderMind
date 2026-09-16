using AIFolderAssistant.Infrastructure.Database;

namespace AIFolderAssistant.Infrastructure.Repository;

public interface ISuggestionRepository
{
    List<Suggestion> GetAll();
    List<Suggestion> GetByFolderPath(string folderPath);
    Suggestion? GetLatest(string folderPath);
    List<Suggestion> GetByStatus(SuggestionStatus status);
    long Add(Suggestion suggestion);
    void UpdateStatus(int id, SuggestionStatus status, DateTime? resolvedAt);
}
