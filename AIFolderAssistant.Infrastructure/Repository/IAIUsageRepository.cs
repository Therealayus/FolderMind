using AIFolderAssistant.Infrastructure.Database;

namespace AIFolderAssistant.Infrastructure.Repository;

public interface IAIUsageRepository
{
    List<AIUsage> GetAll();
    void Add(AIUsage usage);
}
