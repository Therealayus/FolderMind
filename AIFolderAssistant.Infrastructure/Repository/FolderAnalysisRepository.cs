using AIFolderAssistant.Infrastructure.Database;

namespace AIFolderAssistant.Infrastructure.Repository;

public sealed class FolderAnalysisRepository : IFolderAnalysisRepository
{
    private readonly FolderMindDbContext _db;

    public FolderAnalysisRepository(FolderMindDbContext db)
    {
        _db = db;
    }

    public List<FolderAnalysis> GetByFolderPath(string folderPath) =>
        _db.GetFolderAnalyses().Where(a => a.FolderPath.Equals(folderPath, StringComparison.OrdinalIgnoreCase)).ToList();

    public FolderAnalysis? GetLatest(string folderPath) =>
        _db.GetFolderAnalyses()
            .Where(a => a.FolderPath.Equals(folderPath, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(a => a.CreatedAt)
            .FirstOrDefault();

    public void Add(FolderAnalysis analysis) => _db.InsertFolderAnalysis(analysis);
}
