using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AIFolderAssistant.Core.Analysis;
using AIFolderAssistant.Core.BusinessRules;

namespace AIFolderAssistant.Core.Services;

/// <summary>
/// Service that orchestrates folder analysis and suggestion generation.
/// Combines local analysis with optional AI enhancement.
/// </summary>
public class SuggestionService : IFolderAnalyzer
{
    private readonly IFolderAnalyzer _localAnalyzer;
    private readonly IFolderNameGenerator _folderNameGenerator;
    private readonly IConfidenceCalculator _confidenceCalculator;

    public SuggestionService(
        IFolderAnalyzer localAnalyzer,
        IFolderNameGenerator folderNameGenerator,
        IConfidenceCalculator confidenceCalculator)
    {
        _localAnalyzer = localAnalyzer;
        _folderNameGenerator = folderNameGenerator;
        _confidenceCalculator = confidenceCalculator;
    }

    public async Task<FolderAnalysisResult> AnalyzeAsync(string folderPath, CancellationToken cancellationToken)
    {
        // First, try local analysis
        var localResult = await _localAnalyzer.AnalyzeAsync(folderPath, cancellationToken);

        // Use the folder name generator to create a suggestion based on local analysis
        var analysisData = new Analysis.FolderAnalysisData
        {
            Files = new List<Analysis.FileAnalysisResult>(),
            Keywords = new List<string> { localResult.Reason ?? "analyzed folder" },
            DetectedTopics = new List<string> { localResult.SuggestedName ?? "Documents" },
            TotalSize = 0,
            LastModified = DateTime.UtcNow
        };

        var generatedName = _folderNameGenerator.GenerateName(analysisData);
        var confidence = _confidenceCalculator.CalculateConfidence(analysisData);
        var reason = localResult.Reason ?? "Folder analyzed locally";

        return new FolderAnalysisResult
        {
            SuggestedName = generatedName,
            Confidence = confidence,
            Reason = reason,
            AlternativeSuggestions = localResult.AlternativeSuggestions,
            FileCount = localResult.FileCount,
            AnalysisTimeMs = localResult.AnalysisTimeMs,
            UsesCloudAI = false
        };
    }
}