using AIFolderAssistant.Core.Analysis;
using AIFolderAssistant.Core.BusinessRules;
using Xunit;

namespace AIFolderAssistant.Tests;

public sealed class ConfidenceCalculatorTests
{
    [Fact]
    public void EmptyAnalysis_ReturnsZero()
    {
        Assert.Equal(0.0, new ConfidenceCalculator().CalculateConfidence(new FolderAnalysisData()));
    }

    [Fact]
    public void PopulatedAnalysis_ReturnsInRange()
    {
        var data = new FolderAnalysisData();
        for (var i = 0; i < 12; i++)
            data.Files.Add(new FileAnalysisResult { FileName = $"invoice_{i}.pdf", Extension = ".pdf" });
        data.Keywords.AddRange(new[] { "invoice", "receipt", "payment" });
        data.DetectedTopics.Add("Financial");
        data.ExtensionCounts["pdf"] = 12;

        var score = new ConfidenceCalculator().CalculateConfidence(data);
        Assert.InRange(score, 0.0, 1.0);
        Assert.True(score >= 0.5, $"Expected decent confidence, got {score}");
    }

    [Fact]
    public void MoreEvidence_HigherOrEqualConfidence()
    {
        var calc = new ConfidenceCalculator();
        var thin = new FolderAnalysisData();
        thin.Files.Add(new FileAnalysisResult { FileName = "a.pdf", Extension = ".pdf" });
        var rich = new FolderAnalysisData();
        for (var i = 0; i < 10; i++)
            rich.Files.Add(new FileAnalysisResult { FileName = $"invoice_{i}.pdf", Extension = ".pdf" });
        rich.Keywords.AddRange(new[] { "invoice", "receipt" });
        rich.DetectedTopics.Add("Financial");

        Assert.True(calc.CalculateConfidence(rich) >= calc.CalculateConfidence(thin));
    }
}
