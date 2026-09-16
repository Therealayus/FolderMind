using AIFolderAssistant.Infrastructure.Database;

namespace AIFolderAssistant.Infrastructure.Repository;

public sealed class SuggestionRepository : ISuggestionRepository
{
    private readonly FolderMindDbContext _db;

    public SuggestionRepository(FolderMindDbContext db)
    {
        _db = db;
    }

    public List<Suggestion> GetAll() => _db.GetSuggestions();

    public List<Suggestion> GetByFolderPath(string folderPath) =>
        _db.GetSuggestions().Where(s => s.FolderPath.Equals(folderPath, StringComparison.OrdinalIgnoreCase)).ToList();

    public Suggestion? GetLatest(string folderPath) =>
        _db.GetSuggestions()
            .Where(s => s.FolderPath.Equals(folderPath, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(s => s.CreatedAt)
            .FirstOrDefault();

    public List<Suggestion> GetByStatus(SuggestionStatus status) =>
        _db.GetSuggestions().Where(s => s.Status == status).ToList();

    public long Add(Suggestion suggestion) => _db.InsertSuggestion(suggestion);

    public void UpdateStatus(int id, SuggestionStatus status, DateTime? resolvedAt) =>
        _db.UpdateSuggestionStatus(id, status, resolvedAt);
}
