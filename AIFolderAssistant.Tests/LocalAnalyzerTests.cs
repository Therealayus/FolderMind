using AIFolderAssistant.Core.BusinessRules;
using AIFolderAssistant.Core.Engines;
using Xunit;

namespace AIFolderAssistant.Tests;

public sealed class LocalAnalyzerTests : IDisposable
{
    private readonly string _root;

    public LocalAnalyzerTests()
    {
        _root = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "fman-" + Guid.NewGuid().ToString("N"))).FullName;
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { /* best effort */ }
    }

    private static LocalFolderAnalyzer CreateAnalyzer() => new(
        new SimpleMetadataExtractor(),
        new SimpleContentExtractor(),
        new KeywordContentClassifier(),
        new FolderNameGenerator(),
        new ConfidenceCalculator());

    [Fact]
    public async Task Analyze_InvoiceFolder_SuggestsFinancialName()
    {
        var folder = Directory.CreateDirectory(Path.Combine(_root, "New Folder")).FullName;
        File.WriteAllText(Path.Combine(folder, "invoice_123.pdf"), "x");
        File.WriteAllText(Path.Combine(folder, "gst_receipt.pdf"), "x");
        File.WriteAllText(Path.Combine(folder, "payment.txt"), "invoice receipt payment");

        var result = await CreateAnalyzer().AnalyzeAsync(folder, CancellationToken.None);

        Assert.False(string.IsNullOrWhiteSpace(result.SuggestedName));
        Assert.Equal(3, result.FileCount);
        Assert.InRange(result.Confidence, 0.0, 1.0);
        Assert.False(result.UsesCloudAI);
        Assert.False(string.IsNullOrWhiteSpace(result.Reason));
    }

    [Fact]
    public async Task Analyze_MissingFolder_ReturnsInvalidGracefully()
    {
        var result = await CreateAnalyzer().AnalyzeAsync(
            Path.Combine(_root, "DoesNotExist"), CancellationToken.None);
        Assert.Equal(0.0, result.Confidence);
        Assert.Equal(0, result.FileCount);
    }

    [Fact]
    public async Task Analyze_UnsupportedFiles_DoesNotCrash()
    {
        var folder = Directory.CreateDirectory(Path.Combine(_root, "Misc")).FullName;
        File.WriteAllBytes(Path.Combine(folder, "blob.xyz"), new byte[] { 1, 2, 3 });
        var result = await CreateAnalyzer().AnalyzeAsync(folder, CancellationToken.None);
        Assert.Equal(1, result.FileCount);
    }
}
