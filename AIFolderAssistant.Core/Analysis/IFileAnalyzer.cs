namespace AIFolderAssistant.Core.Analysis;

/// <summary>
/// Result of analyzing a single file.
/// </summary>
public class FileAnalysisResult
{
    public string? Extension { get; set; }
    public string? FileName { get; set; }
    public string? DirectoryName { get; set; }
    public long FileSize { get; set; }
    public DateTime CreationTime { get; set; }
    public DateTime LastModifiedTime { get; set; }
    public string? ExtractedText { get; set; }
    public bool Supported { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Extracted file metadata.
/// </summary>
public class Metadata
{
    public string? Title { get; set; }
    public string? Author { get; set; }
    public string? Subject { get; set; }
    public string? Keywords { get; set; }
    public DateTime? CreationDate { get; set; }
    public DateTime? ModificationDate { get; set; }
    public string? AppName { get; set; }
}

/// <summary>
/// Interface for extracting file metadata.
/// </summary>
public interface IMetadataExtractor
{
    /// <summary>
    /// Extracts metadata from a file.
    /// </summary>
    /// <param name="filePath">The path of the file.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task that returns extracted metadata.</returns>
    Task<Metadata> ExtractAsync(string filePath, CancellationToken cancellationToken);
}

/// <summary>
/// Interface for extracting content from files.
/// Supports documents, source code, and other text-based formats.
/// </summary>
public interface IContentExtractor
{
    /// <summary>
    /// Extracts text content from a file.
    /// </summary>
    /// <param name="filePath">The path of the file.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task that returns extracted text.</returns>
    Task<string?> ExtractTextAsync(string filePath, CancellationToken cancellationToken);

    /// <summary>
    /// Gets the file type/extension this extractor supports.
    /// </summary>
    string SupportedExtension { get; }
}

/// <summary>
/// Interface for classifying file content into topics/categories.
/// </summary>
public interface IContentClassifier
{
    /// <summary>
    /// Classifies content and returns detected topics.
    /// </summary>
    /// <param name="text">The text content to classify.</param>
    /// <param name="fileName">The file name for context.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task that returns classified topics with confidence scores.</returns>
    Task<List<ClassifiedTopic>> ClassifyAsync(string text, string fileName, CancellationToken cancellationToken);
}

/// <summary>
/// A classified topic with a confidence score.
/// </summary>
public class ClassifiedTopic
{
    public string Topic { get; set; } = string.Empty;
    public double Confidence { get; set; }
}

/// <summary>
/// Data class for folder analysis.
/// </summary>
public class FolderAnalysisData
{
    public List<FileAnalysisResult> Files { get; set; } = new();
    public List<string> Keywords { get; set; } = new();
    public List<string> DetectedTopics { get; set; } = new();
    public long TotalSize { get; set; }
    public DateTime LastModified { get; set; }
    public Dictionary<string, int> ExtensionCounts { get; set; } = new();
}

/// <summary>
/// Interface for calculating confidence scores for suggestions.
/// </summary>
public interface IConfidenceCalculator
{
    /// <summary>
    /// Calculates confidence score for a folder name suggestion.
    /// </summary>
    /// <param name="analysis">The folder analysis data.</param>
    /// <returns>A confidence score between 0 and 1.</returns>
    double CalculateConfidence(FolderAnalysisData analysis);
}