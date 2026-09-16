using System.Text;
using AIFolderAssistant.Core.Analysis;

namespace AIFolderAssistant.Core.Engines;

/// <summary>
/// File-system metadata extractor (dates, no document parsing).
/// Never throws for missing/locked files — returns best-effort metadata.
/// </summary>
public sealed class SimpleMetadataExtractor : IMetadataExtractor
{
    public Task<Metadata> ExtractAsync(string filePath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var metadata = new Metadata();
        try
        {
            var info = new FileInfo(filePath);
            if (info.Exists)
            {
                metadata.CreationDate = info.CreationTimeUtc;
                metadata.ModificationDate = info.LastWriteTimeUtc;
            }
        }
        catch
        {
            // Best effort only.
        }
        return Task.FromResult(metadata);
    }
}

/// <summary>
/// Text content extractor for plain-text formats (txt/md/csv/json/xml/code).
/// Binary formats (pdf/docx/images) return null — filenames + metadata drive
/// classification there. Reads at most <see cref="MaxChars"/> with a byte cap
/// so huge files never blow up memory.
/// </summary>
public sealed class SimpleContentExtractor : IContentExtractor
{
    public string SupportedExtension => ".txt";

    private const int MaxBytes = 256 * 1024;
    private const int MaxChars = 50_000;

    private static readonly HashSet<string> TextExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".txt", ".md", ".markdown", ".rtf",
        ".csv", ".json", ".xml", ".yaml", ".yml", ".toml", ".ini", ".log",
        ".js", ".ts", ".jsx", ".tsx", ".cs", ".java", ".py", ".go", ".rs",
        ".cpp", ".h", ".hpp", ".sql", ".html", ".css", ".scss", ".ps1", ".bat"
    };

    public static bool IsTextExtension(string extension) => TextExtensions.Contains(extension);

    public async Task<string?> ExtractTextAsync(string filePath, CancellationToken cancellationToken)
    {
        var ext = Path.GetExtension(filePath);
        if (!IsTextExtension(ext))
            return null;
        try
        {
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            var buffer = new byte[Math.Min(MaxBytes, (int)Math.Min(stream.Length, MaxBytes))];
            var read = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (read == 0)
                return string.Empty;
            var text = Encoding.UTF8.GetString(buffer, 0, read);
            return text.Length > MaxChars ? text.Substring(0, MaxChars) : text;
        }
        catch (OperationCanceledException) { throw; }
        catch
        {
            // Locked / deleted / permission-denied files must never crash analysis.
            return null;
        }
    }
}
