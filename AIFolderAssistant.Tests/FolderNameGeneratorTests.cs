using AIFolderAssistant.Core.Analysis;
using AIFolderAssistant.Core.BusinessRules;
using Xunit;

namespace AIFolderAssistant.Tests;

public sealed class FolderNameGeneratorTests
{
    private static FolderAnalysisData DataFor(params string[] fileNames)
    {
        var data = new FolderAnalysisData();
        foreach (var name in fileNames)
        {
            data.Files.Add(new FileAnalysisResult { FileName = name, Extension = Path.GetExtension(name) });
            data.Keywords.Add(Path.GetFileNameWithoutExtension(name));
        }
        return data;
    }

    [Fact]
    public void EmptyFolder_ReturnsFallback()
    {
        var gen = new FolderNameGenerator();
        Assert.Equal("Unnamed Folder", gen.GenerateName(new FolderAnalysisData()));
    }

    [Fact]
    public void InvoiceFiles_SuggestFinancialName()
    {
        var gen = new FolderNameGenerator();
        var name = gen.GenerateName(DataFor("invoice_123.pdf", "invoice_456.pdf", "gst_receipt.pdf", "payment_receipt.pdf"));
        Assert.Contains("Financial", name);
    }

    [Fact]
    public void DevFiles_SuggestDevelopmentName()
    {
        var gen = new FolderNameGenerator();
        var name = gen.GenerateName(DataFor("react.pdf", "nodejs.pdf", "typescript.pdf", "system-design.pdf"));
        Assert.True(name.Contains("Development") || name.Contains("Software") || name.Contains("Web"),
            $"Unexpected suggestion: {name}");
    }

    [Fact]
    public void MixedButCoherentFiles_AggregatesByCategory()
    {
        var gen = new FolderNameGenerator();
        var data = DataFor("invoice_123.pdf", "gst_receipt.pdf", "extra_bill.pdf",
            "notes.txt", "tray_test.txt", "misc.txt", "log.txt", "data.txt");
        data.DetectedTopics.Add("Financial");
        Assert.Contains("Financial", gen.GenerateName(data));
    }

    [Fact]
    public void UnknownFiles_ReturnsNonEmpty()
    {
        var gen = new FolderNameGenerator();
        var name = gen.GenerateName(DataFor("xyz123abc.dat", "qwop456.bin"));
        Assert.False(string.IsNullOrWhiteSpace(name));
    }
}
