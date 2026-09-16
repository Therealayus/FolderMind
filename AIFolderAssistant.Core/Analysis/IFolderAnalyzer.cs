namespace AIFolderAssistant.Core.Analysis;

/// <summary>
/// Result of analyzing a folder.
/// </summary>
public class FolderAnalysisResult
{
    public string SuggestedName { get; set; } = string.Empty;
    public double Confidence { get; set; }
    public string Reason { get; set; } = string.Empty;
    public List<string> AlternativeSuggestions { get; set; } = new();
    public int FileCount { get; set; }
    public double AnalysisTimeMs { get; set; }
    public bool UsesCloudAI { get; set; }
}

/// <summary>
/// Interface for analyzing folders and suggesting names.
/// Implements the analysis pipeline from Phase 10.
/// </summary>
public interface IFolderAnalyzer
{
    /// <summary>
    /// Analyzes a folder and generates a name suggestion.
    /// </summary>
    /// <param name="folderPath">The path of the folder to analyze.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task that returns the analysis result.</returns>
    Task<FolderAnalysisResult> AnalyzeAsync(string folderPath, CancellationToken cancellationToken);
}

/// <summary>
/// Interface for generating folder names based on analysis data.
/// </summary>
public interface IFolderNameGenerator
{
    /// <summary>
    /// Generates a folder name suggestion based on analysis.
    /// </summary>
    /// <param name="analysis">The folder analysis data.</param>
    /// <returns>The suggested folder name.</returns>
    string GenerateName(FolderAnalysisData analysis);
}

/// <summary>
/// Interface for the complete folder analysis pipeline.
/// Composes all the smaller interfaces into a single processing pipeline.
/// </summary>
public interface IFolderAnalysisPipeline
{
    /// <summary>
    /// Runs the complete analysis pipeline on a folder.
    /// </summary>
    /// <param name="folderPath">The path of the folder to analyze.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task that returns the analysis result.</returns>
    Task<FolderAnalysisResult> RunPipelineAsync(string folderPath, CancellationToken cancellationToken);
}