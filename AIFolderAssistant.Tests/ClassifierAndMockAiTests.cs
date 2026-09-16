using AIFolderAssistant.Core.AI;
using AIFolderAssistant.Core.Analysis;
using AIFolderAssistant.Core.Engines;
using Xunit;

namespace AIFolderAssistant.Tests;

public sealed class ClassifierAndMockAiTests
{
    [Fact]
    public async Task Classifier_DetectsFinancialTopic()
    {
        var classifier = new KeywordContentClassifier();
        var topics = await classifier.ClassifyAsync("invoice receipt payment gst tax", "invoice_123.pdf", CancellationToken.None);
        Assert.Contains(topics, t => t.Topic == "Financial");
    }

    [Fact]
    public async Task Classifier_EmptyText_ReturnsEmpty()
    {
        var classifier = new KeywordContentClassifier();
        var topics = await classifier.ClassifyAsync("lorem ipsum dolor", "zzz.txt", CancellationToken.None);
        Assert.Empty(topics);
    }

    [Fact]
    public async Task MockAi_InvoiceKeywords_ReturnsFinancialDocuments()
    {
        var ai = new MockAIProvider();
        var analysis = new FolderAnalysisData
        {
            Keywords = new List<string> { "invoice", "receipt", "payment" },
            DetectedTopics = new List<string> { "Financial" }
        };
        var result = await ai.GenerateFolderNameAsync(analysis, CancellationToken.None);
        Assert.Equal("Financial Documents", result.Suggestion);
        Assert.InRange(result.Confidence, 0.0, 1.0);
        Assert.NotEmpty(result.Reason);
    }

    [Fact]
    public async Task MockAi_RespectsCancellation()
    {
        var ai = new MockAIProvider();
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => ai.GenerateFolderNameAsync(new FolderAnalysisData(), cts.Token));
    }
}
