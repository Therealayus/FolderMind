using AIFolderAssistant.Infrastructure.Database;
using AIFolderAssistant.Infrastructure.Repository;
using Xunit;

namespace AIFolderAssistant.Tests;

/// <summary>
/// SQLite persistence tests against an isolated temp database file.
/// </summary>
public sealed class RepositoryTests : IDisposable
{
    private readonly string _dbPath;
    private readonly FolderMindDbContext _db;

    public RepositoryTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), "fmdb-" + Guid.NewGuid().ToString("N") + ".db");
        _db = new FolderMindDbContext(_dbPath);
    }

    public void Dispose()
    {
        try { File.Delete(_dbPath); } catch { /* best effort */ }
    }

    [Fact]
    public void SchemaInit_IsIdempotent()
    {
        // Second construction against the same file must not throw and must
        // see data written through the first context.
        var again = new FolderMindDbContext(_dbPath);
        again.InsertOrUpdateSetting(new Setting { Key = "k", Value = "v", LastModified = DateTime.UtcNow });
        Assert.Equal("v", _db.GetSetting("k"));
    }

    [Fact]
    public void MonitoredFolders_Crud()
    {
        var repo = new MonitoredFolderRepository(_db);
        var now = DateTime.UtcNow;
        repo.Add(new MonitoredFolder { Path = @"C:\Demo", IsEnabled = true, CreatedAt = now, UpdatedAt = now, LastScannedAt = default });
        Assert.NotNull(repo.GetByPath(@"c:\demo"));
        Assert.Single(repo.GetAll());

        var e = repo.GetByPath(@"C:\Demo")!;
        e.IsEnabled = false;
        e.UpdatedAt = now;
        e.LastScannedAt = now;
        repo.Update(e);
        Assert.False(repo.GetByPath(@"C:\Demo")!.IsEnabled);

        repo.Delete(@"C:\Demo");
        Assert.Empty(repo.GetAll());
    }

    [Fact]
    public void Suggestions_Lifecycle()
    {
        var repo = new SuggestionRepository(_db);
        var id = (int)repo.Add(new Suggestion
        {
            FolderPath = @"C:\Demo\New Folder",
            OriginalName = "New Folder",
            SuggestedName = "Financial Documents",
            Confidence = 0.9,
            Reason = "test",
            Status = SuggestionStatus.Pending,
            CreatedAt = DateTime.UtcNow
        });
        Assert.Equal("Financial Documents", repo.GetLatest(@"C:\Demo\New Folder")!.SuggestedName);
        Assert.Single(repo.GetByStatus(SuggestionStatus.Pending));
        repo.UpdateStatus(id, SuggestionStatus.Accepted, DateTime.UtcNow);
        Assert.Empty(repo.GetByStatus(SuggestionStatus.Pending));
        Assert.Single(repo.GetByStatus(SuggestionStatus.Accepted));
    }

    [Fact]
    public void RenameHistory_LookupByPostRenamePath()
    {
        // Regression: records store the pre-rename path, but undo queries
        // with the post-rename path (parent + NewName).
        var repo = new RenameHistoryRepository(_db);
        repo.Add(new RenameHistory
        {
            FolderPath = @"C:\Demo\New Folder",
            OriginalName = "New Folder",
            NewName = "Financial Documents",
            Success = true,
            CreatedAt = DateTime.UtcNow
        });
        var found = repo.GetLastForFolder(@"C:\Demo\Financial Documents");
        Assert.NotNull(found);
        Assert.Equal("New Folder", found!.OriginalName);
    }

    [Fact]
    public void Settings_RoundTrip()
    {
        var repo = new SettingsRepository(_db);
        Assert.Null(repo.Get("missing"));
        repo.Set("a", "1");
        Assert.Equal("1", repo.Get("a"));
        repo.Set("a", "2");
        Assert.Equal("2", repo.Get("a"));
    }
}
