using AIFolderAssistant.Core.Analysis;

namespace AIFolderAssistant.Core.BusinessRules;

/// <summary>
/// Calculates confidence score for a folder name suggestion based on analysis data.
/// Uses heuristics based on keyword matching, file type clustering, and other factors.
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

        var score = 0.0;
        var totalWeight = 0.0;

        // Factor 1: File count weight (max 0.3)
        var fileCountFactor = Math.Min(analysis.Files.Count / 10.0, 1.0);
        score += fileCountFactor * 0.3;
        totalWeight += 0.3;

        // Factor 2: Extension distribution (max 0.3)
        var extensionDiversity = analysis.ExtensionCounts.Count;
        var extensionFactor = Math.Min(extensionDiversity / 5.0, 1.0);
        score += extensionFactor * 0.3;
        totalWeight += 0.3;

        // Factor 3: Keyword specificity (max 0.2)
        var keywordScore = analysis.Keywords.Count > 0 ? Math.Min(analysis.Keywords.Count / 3.0, 1.0) : 0.0;
        score += keywordScore * 0.2;
        totalWeight += 0.2;

        // Factor 4: Topic detection (max 0.2)
        var topicScore = analysis.DetectedTopics.Count > 0 ? Math.Min(analysis.DetectedTopics.Count / 3.0, 1.0) : 0.0;
        score += topicScore * 0.2;
        totalWeight += 0.2;

        // Avoid division by zero
        if (totalWeight <= 0)
            return 0.0;

        var finalScore = Math.Round(score / totalWeight, 2);
        return Math.Clamp(finalScore, 0.0, 1.0);
    }
}