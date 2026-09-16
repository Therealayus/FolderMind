using AIFolderAssistant.Core.Analysis;

namespace AIFolderAssistant.Core.BusinessRules;

/// <summary>
/// Calculates confidence score for a folder name suggestion based on analysis data.
/// Calibrated so a small folder with clear keyword/topic agreement scores ≥ 0.80
/// (the default notify threshold), while thin or conflicting evidence stays low.
/// </summary>
public class ConfidenceCalculator : IConfidenceCalculator
{
    /// <summary>
    /// Calculates confidence score for a folder name suggestion.
    /// </summary>
    /// <param name="analysis">The folder analysis data.</param>
    /// <returns>A confidence score between 0 and 1.</returns>
    public double CalculateConfidence(FolderAnalysisData analysis)
    {
        if (analysis.Files.Count == 0)
            return 0.0;

        // File count: saturates fast — 4+ files carry full weight (0.25).
        var fileScore = Math.Min(analysis.Files.Count / 4.0, 1.0) * 0.25;

        // Extension coherence: a dominant type (or small set) is a good sign (0.15).
        var extensionScore = analysis.ExtensionCounts.Count > 0
            ? Math.Min(analysis.ExtensionCounts.Count / 3.0, 1.0) * 0.15
            : 0.0;

        // Keyword evidence: the strongest signal (0.35).
        var keywordScore = analysis.Keywords.Count > 0
            ? Math.Min(analysis.Keywords.Count / 2.0, 1.0) * 0.35
            : 0.0;

        // Topic agreement: detected categories reinforce the suggestion (0.25).
        var topicScore = analysis.DetectedTopics.Count > 0
            ? Math.Min(analysis.DetectedTopics.Count / 1.0, 1.0) * 0.25
            : 0.0;

        return Math.Clamp(Math.Round(fileScore + extensionScore + keywordScore + topicScore, 2), 0.0, 1.0);
    }
}
