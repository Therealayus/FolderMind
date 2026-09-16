using System.Security.Cryptography;
using System.Text;
using AIFolderAssistant.Core.Interfaces;

namespace AIFolderAssistant.Infrastructure.Security;

/// <summary>
/// Per-user secret storage using DPAPI (<see cref="ProtectedData"/>) with
/// additional entropy, persisted as files under %AppData%\FolderMind\secrets.
/// Secrets are encrypted for the current Windows user and never stored in plain text.
/// </summary>
public sealed class DpapiSecretStore : ISecretStore
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("FolderMind.AI.v1.");
    private readonly string _directory;

    public DpapiSecretStore(string? directory = null)
    {
        _directory = directory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "FolderMind", "secrets");
        Directory.CreateDirectory(_directory);
    }

    public void SetSecret(string name, string value)
    {
        ValidateName(name);
        var plain = Encoding.UTF8.GetBytes(value);
        var encrypted = ProtectedData.Protect(plain, Entropy, DataProtectionScope.CurrentUser);
        CryptographicOperations.ZeroMemory(plain);
        File.WriteAllBytes(GetPath(name), encrypted);
    }

    public string? GetSecret(string name)
    {
        ValidateName(name);
        var path = GetPath(name);
        if (!File.Exists(path))
            return null;
        try
        {
            var encrypted = File.ReadAllBytes(path);
            var plain = ProtectedData.Unprotect(encrypted, Entropy, DataProtectionScope.CurrentUser);
            var value = Encoding.UTF8.GetString(plain);
            CryptographicOperations.ZeroMemory(plain);
            return value;
        }
        catch (CryptographicException)
        {
            // Wrong user profile or corrupted store — treat as missing, never crash.
            return null;
        }
    }

    public void DeleteSecret(string name)
    {
        ValidateName(name);
        var path = GetPath(name);
        if (File.Exists(path))
            File.Delete(path);
    }

    private string GetPath(string name)
    {
        var safe = string.Concat(name.Where(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_' or '.'));
        return Path.Combine(_directory, safe + ".bin");
    }

    private static void ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Secret name must not be empty.", nameof(name));
    }
}
