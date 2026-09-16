using AIFolderAssistant.Infrastructure.Database;

namespace AIFolderAssistant.Infrastructure.Repository;

public sealed class AIUsageRepository : IAIUsageRepository
{
    private readonly FolderMindDbContext _db;

    public AIUsageRepository(FolderMindDbContext db)
    {
        _db = db;
    }

    public List<AIUsage> GetAll() => _db.GetAIUsage();

    public void Add(AIUsage usage) => _db.InsertAIUsage(usage);
}
