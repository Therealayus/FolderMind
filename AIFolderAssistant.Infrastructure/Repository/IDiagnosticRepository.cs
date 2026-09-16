using AIFolderAssistant.Infrastructure.Database;

namespace AIFolderAssistant.Infrastructure.Repository;

public interface IDiagnosticRepository
{
    List<Diagnostic> GetRecent(int maxRows = 500);
    void Add(Diagnostic diagnostic);
    void Clear();
}
