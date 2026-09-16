using AIFolderAssistant.Infrastructure.Database;

namespace AIFolderAssistant.Infrastructure.Repository;

public sealed class SettingsRepository : ISettingsRepository
{
    private readonly FolderMindDbContext _db;

    public SettingsRepository(FolderMindDbContext db)
    {
        _db = db;
    }

    public string? Get(string key) => _db.GetSetting(key);

    public void Set(string key, string value) =>
        _db.InsertOrUpdateSetting(new Setting { Key = key, Value = value, LastModified = DateTime.UtcNow });

    public List<KeyValuePair<string, string>> GetAll() => _db.GetAllSettings();

    public void Reset() => _db.ResetSettings();
}
