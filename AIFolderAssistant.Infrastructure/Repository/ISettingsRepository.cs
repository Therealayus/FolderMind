namespace AIFolderAssistant.Infrastructure.Repository;

public interface ISettingsRepository
{
    string? Get(string key);
    void Set(string key, string value);
    List<KeyValuePair<string, string>> GetAll();
    void Reset();
}
