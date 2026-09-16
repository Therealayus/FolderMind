using AIFolderAssistant.Infrastructure.Database;

namespace AIFolderAssistant.Infrastructure.Repository;

public sealed class DiagnosticRepository : IDiagnosticRepository
{
    private readonly FolderMindDbContext _db;

    public DiagnosticRepository(FolderMindDbContext db)
    {
        _db = db;
    }

    public List<Diagnostic> GetRecent(int maxRows = 500) => _db.GetDiagnostics(maxRows);

    public void Add(Diagnostic diagnostic) => _db.InsertDiagnostic(diagnostic);

    public void Clear() => _db.ClearDiagnostics();
}
