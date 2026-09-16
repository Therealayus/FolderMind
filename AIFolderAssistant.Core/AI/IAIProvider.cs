using System;
using System.Threading;
using System.Threading.Tasks;
using AIFolderAssistant.Core.Analysis;

namespace AIFolderAssistant.Core.AI;

/// <summary>
/// Interface for AI providers that can generate folder name suggestions.
/// Supports multiple AI providers (OpenAI, Local Model, Azure/OpenAI-compatible, etc.).
/// </summary>
public interface IAIProvider
{
    /// <summary>
    /// Generates a folder name suggestion using AI.
    /// </summary>
    /// <param name="analysis">The folder analysis data containing file information, keywords, and topics.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task that returns the AI-generated folder name suggestion.</returns>
    Task<FolderNameSuggestion> GenerateFolderNameAsync(FolderAnalysisData analysis, CancellationToken cancellationToken);
}

/// <summary>
/// Structured result from AI provider for folder name generation.
/// </summary>
public struct FolderNameSuggestion
{
    /// <summary>
    /// Default constructor.
    /// </summary>
    public FolderNameSuggestion()
    {
        Suggestion = string.Empty;
        Alternatives = new List<string>();
        Confidence = 0.0;
        Reason = string.Empty;
    }

    /// <summary>
    /// The primary suggested folder name.
    /// </summary>
    public string Suggestion { get; set; } = string.Empty;

    /// <summary>
    /// Alternative folder name suggestions.
    /// </summary>
    public List<string> Alternatives { get; set; } = new();

    /// <summary>
    /// Confidence score between 0 and 1.
    /// </summary>
    public double Confidence { get; set; }

    /// <summary>
    /// Reason explaining why this suggestion was generated.
    /// </summary>
    public string Reason { get; set; } = string.Empty;
}

/// <summary>
/// Mock AI provider for testing and offline operation.
/// Generates suggestions based on local keyword analysis without calling external APIs.
/// </summary>
public class MockAIProvider : IAIProvider
{
    /// <summary>
    /// Generates a folder name suggestion using mock AI analysis.
    /// </summary>
    /// <param name="analysis">The folder analysis data.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task that returns the mock AI-generated suggestion.</returns>
    public async Task<FolderNameSuggestion> GenerateFolderNameAsync(FolderAnalysisData analysis, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Simulate some processing time
        await Task.Delay(100, cancellationToken);

        // Use local analysis to generate a suggestion
        var keywords = analysis.Keywords ?? new List<string>();
        var topics = analysis.DetectedTopics ?? new List<string>();

        var suggestion = "Documents";
        var alternatives = new List<string> { "Documents", "Photos", "Work Files" };
        var confidence = 0.85;
        var reason = "Based on file analysis";

        // Check for specific keywords to generate better suggestions
        if (keywords.Any(k => k.Equals("invoice", StringComparison.OrdinalIgnoreCase) ||
                            k.Equals("receipt", StringComparison.OrdinalIgnoreCase) ||
                            k.Equals("payment", StringComparison.OrdinalIgnoreCase)))
        {
            suggestion = "Financial Documents";
            alternatives = new List<string> { "Financial Documents", "Invoices & Receipts", "Payment Records" };
            confidence = 0.95;
            reason = "Most files relate to invoices, receipts and financial transactions";
        }
        else if (keywords.Any(k => k.Equals("react", StringComparison.OrdinalIgnoreCase) ||
                                  k.Equals("javascript", StringComparison.OrdinalIgnoreCase) ||
                                  k.Equals("node", StringComparison.OrdinalIgnoreCase) ||
                                  k.Equals("typescript", StringComparison.OrdinalIgnoreCase)))
        {
            suggestion = "Web Development";
            alternatives = new List<string> { "Web Development", "Software Development", "JavaScript Projects" };
            confidence = 0.90;
            reason = "Files relate to web development and programming technologies";
        }
        else if (keywords.Any(k => k.Equals("goa", StringComparison.OrdinalIgnoreCase) ||
                                  k.Equals("trip", StringComparison.OrdinalIgnoreCase) ||
                                  k.Equals("vacation", StringComparison.OrdinalIgnoreCase)))
        {
            suggestion = "Goa Trip";
            alternatives = new List<string> { "Goa Trip", "Trip Photos", "Travel Documentation" };
            confidence = 0.92;
            reason = "Files appear to be from a trip or vacation";
        }
        else if (topics.Any())
        {
            suggestion = topics.First();
            alternatives = topics.Take(3).ToList();
            confidence = 0.88;
            reason = $"Detected topic: {string.Join(", ", topics.Take(3))}";
        }
        else
        {
            // Default based on file extensions
            var ext = analysis.Files?.FirstOrDefault()?.Extension;
            if (ext != null)
            {
                switch (ext.ToLowerInvariant())
                {
                    case ".pdf":
                        suggestion = "Financial Documents";
                        break;
                    case ".jpg":
                    case ".png":
                        suggestion = "Photo Gallery";
                        break;
                    case ".cs":
                    case ".java":
                    case ".py":
                        suggestion = "Code Projects";
                        break;
                }
            }
            confidence = 0.75;
            reason = "Based on file types and names";
        }

        return new FolderNameSuggestion
        {
            Suggestion = suggestion,
            Alternatives = alternatives.Distinct().ToList(),
            Confidence = confidence,
            Reason = reason
        };
    }
}