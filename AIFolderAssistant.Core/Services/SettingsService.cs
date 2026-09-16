using System.Text.Json;
using AIFolderAssistant.Core.Configuration;
using AIFolderAssistant.Core.Interfaces;

namespace AIFolderAssistant.Core.Services;

/// <summary>
/// Strongly-typed settings persisted as JSON blobs in <see cref="ISettingsStore"/>.
/// </summary>
public sealed class SettingsService : ISettingsService
{
    private readonly ISettingsStore _store;
    private const string RootKey = "AppSettings";

    public SettingsService(ISettingsStore store)
    {
        _store = store;
    }

    public AppSettings Current { get; private set; } = new();

    public event EventHandler<AppSettings>? Changed;

    public void Load()
    {
        var raw = _store.Get(RootKey);
        if (!string.IsNullOrWhiteSpace(raw))
        {
            try
            {
                Current = JsonSerializer.Deserialize<AppSettings>(raw) ?? new AppSettings();
                return;
            }
            catch
            {
                // Corrupt settings fall back to defaults.
            }
        }
        Current = new AppSettings();
    }

    public void Save()
    {
        _store.Set(RootKey, JsonSerializer.Serialize(Current));
        Changed?.Invoke(this, Current);
    }

    public void ResetToDefaults()
    {
        Current = new AppSettings();
        Save();
    }
}
