using AIFolderAssistant.Core.Interfaces;

namespace AIFolderAssistant.Infrastructure.Repository;

/// <summary>
/// Bridges <see cref="ISettingsStore"/> (Core) to the SQLite-backed
/// <see cref="ISettingsRepository"/> so Core services never touch Infra types directly.
/// </summary>
public sealed class SettingsStoreAdapter : ISettingsStore
{
    private readonly ISettingsRepository _repository;

    public SettingsStoreAdapter(ISettingsRepository repository)
    {
        _repository = repository;
    }

    public string? Get(string key) => _repository.Get(key);

    public void Set(string key, string value) => _repository.Set(key, value);
}
