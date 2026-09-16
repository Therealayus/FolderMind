using AIFolderAssistant.Core;
using AIFolderAssistant.Core.BusinessRules;
using AIFolderAssistant.Core.Engines;
using Xunit;

namespace AIFolderAssistant.Tests;

public sealed class LargeFolderTests : IDisposable
{
    private readonly string _root;

    public LargeFolderTests()
    {
        _root = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "fmlarge-" + Guid.NewGuid().ToString("N"))).FullName;
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

    private static void Touch(string path) => File.WriteAllBytes(path, Array.Empty<byte>());

    [Fact]
    public async Task ThousandFileFolder_IsCappedAndSampled()
    {
        for (var i = 0; i < 1200; i++)
            Touch(Path.Combine(_root, $"invoice_{i:D4}.pdf"));

        var result = await CreateAnalyzer().AnalyzeAsync(_root, CancellationToken.None);

        Assert.Equal(FolderMindLimits.DefaultMaxFiles, result.FileCount);
        Assert.Equal(1200, result.TotalFileCount);
        Assert.True(result.WasSampled);
        Assert.Contains("sample", result.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Financial", result.SuggestedName);
    }

    [Fact]
    public async Task MaxFilesParameter_IsRespected()
    {
        for (var i = 0; i < 100; i++)
            Touch(Path.Combine(_root, $"file_{i:D3}.txt"));

        var result = await CreateAnalyzer().AnalyzeAsync(_root, CancellationToken.None, maxFiles: 50);

        Assert.True(result.FileCount <= 50);
        Assert.Equal(100, result.TotalFileCount);
        Assert.True(result.WasSampled);
    }

    [Fact]
    public void StrideSampling_SpansWholeTree()
    {
        // Distinct stems per subdir: a fair sample must see all three.
        foreach (var d in new[] { "alpha", "beta", "gamma" })
        {
            var dir = Directory.CreateDirectory(Path.Combine(_root, d)).FullName;
            for (var i = 0; i < 100; i++)
                Touch(Path.Combine(dir, $"{d}_{i:D3}.txt"));
        }

        var discovered = LocalFolderAnalyzer.SafeEnumerateFiles(_root, CancellationToken.None);
        Assert.Equal(300, discovered.Count);

        var sampled = LocalFolderAnalyzer.SampleEvenly(discovered, 30);
        Assert.Equal(30, sampled.Count);
        Assert.Contains(sampled, p => p.Contains("alpha"));
        Assert.Contains(sampled, p => p.Contains("beta"));
        Assert.Contains(sampled, p => p.Contains("gamma"));

        // First-N clusters in whatever the filesystem returns first; stride spans all.
        var firstN = discovered.Take(30).ToList();
        var firstNDirs = firstN.Select(p => Path.GetFileName(Path.GetDirectoryName(p))).Distinct().ToList();
        Assert.Single(firstNDirs);
    }

    [Fact]
    public void SafeEnumerate_SkipsBadSubtrees()
    {
        var good = Directory.CreateDirectory(Path.Combine(_root, "good")).FullName;
        Touch(Path.Combine(good, "ok.txt"));
        // A file masquerading where GetDirectories may trip: still must not throw.
        Touch(Path.Combine(_root, "blocker.txt"));

        var paths = LocalFolderAnalyzer.SafeEnumerateFiles(_root, CancellationToken.None);

        Assert.Contains(paths, p => p.EndsWith("ok.txt"));
        Assert.Contains(paths, p => p.EndsWith("blocker.txt"));
    }

    [Fact]
    public async Task EmptyFolder_ReportsZeroTotals()
    {
        var result = await CreateAnalyzer().AnalyzeAsync(_root, CancellationToken.None);
        Assert.Equal(0, result.FileCount);
        Assert.Equal(0, result.TotalFileCount);
        Assert.False(result.WasSampled);
    }
}
