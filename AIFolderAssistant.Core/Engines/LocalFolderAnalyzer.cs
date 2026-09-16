using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AIFolderAssistant.Core.Analysis;
using AIFolderAssistant.Core.BusinessRules;

namespace AIFolderAssistant.Core.Engines;

/// <summary>
/// Local file system analysis engine that works offline without AI providers.
/// Implements the analysis pipeline using local file metadata and keyword classification.
/// </summary>
public class LocalFolderAnalyzer : IFolderAnalyzer
{
    private readonly IMetadataExtractor _metadataExtractor;
    private readonly IContentExtractor _contentExtractor;
    private readonly IContentClassifier _contentClassifier;
    private readonly IFolderNameGenerator _folderNameGenerator;
    private readonly IConfidenceCalculator _confidenceCalculator;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _folderLocks = new();

    public LocalFolderAnalyzer(
        IMetadataExtractor metadataExtractor,
        IContentExtractor contentExtractor,
        IContentClassifier contentClassifier,
        IFolderNameGenerator folderNameGenerator,
        IConfidenceCalculator confidenceCalculator)
    {
        _metadataExtractor = metadataExtractor;
        _contentExtractor = contentExtractor;
        _contentClassifier = contentClassifier;
        _folderNameGenerator = folderNameGenerator;
        _confidenceCalculator = confidenceCalculator;
    }

    public async Task<FolderAnalysisResult> AnalyzeAsync(
        string folderPath, CancellationToken cancellationToken, int maxFiles = FolderMindLimits.DefaultMaxFiles)
    {
        cancellationToken.ThrowIfCancellationRequested();
        maxFiles = FolderMindLimits.ClampMaxFiles(maxFiles);

        // Ensure folder exists
        if (!Directory.Exists(folderPath))
        {
            return new FolderAnalysisResult
            {
                SuggestedName = "Invalid Folder",
                Confidence = 0.0,
                Reason = "The specified folder does not exist.",
                AlternativeSuggestions = new List<string> { "Please select a valid folder" },
                FileCount = 0,
                TotalFileCount = 0,
                AnalysisTimeMs = 0,
                UsesCloudAI = false
            };
        }

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var analysisData = new FolderAnalysisData();

        try
        {
            // Bounded defensive discovery: inaccessible subdirectories are skipped
            // (never abort the scan), enumeration stops at a hard path cap.
            var discovered = SafeEnumerateFiles(folderPath, cancellationToken);

            // Even stride-sample so huge folders are represented across the whole
            // tree instead of biasing to whatever the filesystem returns first.
            var filesToAnalyze = SampleEvenly(discovered, maxFiles);
            var sampled = filesToAnalyze.Count < discovered.Count;

            long totalSize = 0;
            foreach (var f in filesToAnalyze)
            {
                try { totalSize += new FileInfo(f).Length; } catch { /* vanished; ignore */ }
            }
            analysisData.TotalSize = totalSize;

            foreach (var filePath in filesToAnalyze)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var fileInfo = new FileInfo(filePath);
                var fileName = fileInfo.Name;

                // Analyze each file
                var fileResult = await AnalyzeFileAsync(filePath, cancellationToken);
                if (fileResult != null)
                {
                    analysisData.Files.Add(fileResult);

                    // Extract keywords from file name
                    var nameKeywords = ExtractKeywordsFromName(fileName);
                    foreach (var keyword in nameKeywords)
                    {
                        analysisData.Keywords.Add(keyword);
                    }
                }

                // Get metadata
                try
                {
                    var metadata = await _metadataExtractor.ExtractAsync(filePath, cancellationToken);
                    if (metadata != null)
                    {
                        // Could use metadata for additional classification
                    }
                }
                catch
                {
                    // Ignore metadata extraction errors
                }

                // Classify content if we have extracted text
                try
                {
                    var extractedText = await _contentExtractor.ExtractTextAsync(filePath, cancellationToken);
                    if (!string.IsNullOrEmpty(extractedText))
                    {
                        var topics = await _contentClassifier.ClassifyAsync(
                            extractedText, fileName, cancellationToken);
                        foreach (var topic in topics)
                        {
                            if (!analysisData.DetectedTopics.Contains(topic.Topic))
                                analysisData.DetectedTopics.Add(topic.Topic);
                        }
                    }
                }
                catch
                {
                    // Ignore classification errors
                }
            }

            // Update extension counts
            foreach (var file in analysisData.Files)
            {
                if (!string.IsNullOrEmpty(file.Extension))
                {
                    var ext = file.Extension.TrimStart('.').ToLowerInvariant();
                    if (analysisData.ExtensionCounts.ContainsKey(ext))
                        analysisData.ExtensionCounts[ext]++;
                    else
                        analysisData.ExtensionCounts[ext] = 1;
                }
            }

            // Generate folder name suggestion
            var suggestedName = _folderNameGenerator.GenerateName(analysisData);
            var confidence = _confidenceCalculator.CalculateConfidence(analysisData);
            var reason = GenerateReason(analysisData);
            if (sampled)
                reason += $" (representative sample of {filesToAnalyze.Count} of {discovered.Count} files)";

            stopwatch.Stop();

            return new FolderAnalysisResult
            {
                SuggestedName = suggestedName,
                Confidence = confidence,
                Reason = reason,
                AlternativeSuggestions = GenerateAlternativeSuggestions(analysisData),
                FileCount = analysisData.Files.Count,
                TotalFileCount = discovered.Count,
                AnalysisTimeMs = stopwatch.ElapsedMilliseconds,
                UsesCloudAI = false
            };
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            return new FolderAnalysisResult
            {
                SuggestedName = "Analysis Cancelled",
                Confidence = 0.0,
                Reason = "Folder analysis was cancelled.",
                AlternativeSuggestions = new List<string> { "Try again with a smaller folder" },
                FileCount = analysisData?.Files.Count ?? 0,
                TotalFileCount = analysisData?.Files.Count ?? 0,
                AnalysisTimeMs = stopwatch.ElapsedMilliseconds,
                UsesCloudAI = false
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return new FolderAnalysisResult
            {
                SuggestedName = "Analysis Error",
                Confidence = 0.0,
                Reason = $"An error occurred during analysis: {ex.Message}",
                AlternativeSuggestions = new List<string> { "Try a different folder" },
                FileCount = analysisData?.Files.Count ?? 0,
                TotalFileCount = analysisData?.Files.Count ?? 0,
                AnalysisTimeMs = stopwatch.ElapsedMilliseconds,
                UsesCloudAI = false
            };
        }
    }

    /// <summary>
    /// Iterative breadth-tolerant file discovery. Each directory is enumerated
    /// independently: denied, missing, or misbehaving subtrees are skipped so a
    /// single bad folder can never abort the scan. Bounded to
    /// <see cref="FolderMindLimits.MaxDiscoveredPaths"/> paths; system/hidden
    /// entries are filtered out. Never throws for filesystem conditions
    /// (only for cancellation).
    /// </summary>
    internal static List<string> SafeEnumerateFiles(string root, CancellationToken cancellationToken)
    {
        var paths = new List<string>();
        var stack = new Stack<string>();
        stack.Push(root);

        while (stack.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var dir = stack.Pop();

            string[] subdirs;
            string[] files;
            try
            {
                subdirs = Directory.GetDirectories(dir);
            }
            catch (UnauthorizedAccessException) { continue; }
            catch (DirectoryNotFoundException) { continue; }
            catch (IOException) { continue; }

            try
            {
                files = Directory.GetFiles(dir);
            }
            catch (UnauthorizedAccessException) { files = Array.Empty<string>(); }
            catch (DirectoryNotFoundException) { continue; }
            catch (IOException) { files = Array.Empty<string>(); }

            foreach (var file in files)
            {
                if (paths.Count >= FolderMindLimits.MaxDiscoveredPaths)
                    return paths;
                if (!IsSystemFile(file))
                    paths.Add(file);
            }

            foreach (var sub in subdirs)
            {
                if (paths.Count >= FolderMindLimits.MaxDiscoveredPaths)
                    return paths;
                if (!IsSystemDirectory(sub))
                    stack.Push(sub);
            }
        }

        return paths;
    }

    /// <summary>
    /// Even stride-sample: when discovery exceeds the cap, picks files spread
    /// across the whole enumeration instead of the first N. Deterministic.
    /// </summary>
    internal static List<string> SampleEvenly(List<string> paths, int maxFiles)
    {
        if (paths.Count <= maxFiles)
            return paths;
        var stride = (double)paths.Count / maxFiles;
        var sampled = new List<string>(maxFiles);
        for (var i = 0; i < maxFiles; i++)
            sampled.Add(paths[(int)(i * stride)]);
        return sampled;
    }

    private static bool IsSystemDirectory(string dir)
    {
        try
        {
            var attributes = File.GetAttributes(dir);
            if ((attributes & FileAttributes.System) != 0 || (attributes & FileAttributes.Hidden) != 0)
                return true;
            var name = Path.GetFileName(dir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            return name.StartsWith(".", StringComparison.Ordinal) || BusinessRules.PathValidator.IsSystemDirectory(dir);
        }
        catch
        {
            return true;
        }
    }

    /// <summary>
    /// Checks if a file is a system file and should be excluded from analysis.
    /// </summary>
    private static bool IsSystemFile(string filePath)
    {
        try
        {
            var attributes = File.GetAttributes(filePath);
            return (attributes & FileAttributes.System) == FileAttributes.System
                   || (attributes & FileAttributes.Hidden) == FileAttributes.Hidden
                   || Path.GetFileName(filePath).StartsWith(".")
                   || Path.GetFileName(filePath).StartsWith("~");
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Analyzes a single file and returns analysis results.
    /// </summary>
    /// <param name="filePath">The path of the file to analyze.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>File analysis result, or null if the file is unsupported.</returns>
    private async Task<FileAnalysisResult?> AnalyzeFileAsync(string filePath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var extension = Path.GetExtension(filePath);
        if (string.IsNullOrEmpty(extension))
            return null;

        // Check if the extension is supported
        var supportedExtension = GetSupportedExtension(extension);
        if (supportedExtension == null)
        {
            // Return unsupported file info
            return new FileAnalysisResult
            {
                Extension = extension,
                FileName = Path.GetFileName(filePath),
                FileSize = new FileInfo(filePath).Length,
                CreationTime = File.GetCreationTime(filePath),
                LastModifiedTime = File.GetLastWriteTime(filePath),
                Supported = false,
                ErrorMessage = $"File type '{extension}' is not supported for content analysis."
            };
        }

        var fileInfo = new FileInfo(filePath);
        var result = new FileAnalysisResult
        {
            Extension = extension,
            FileName = fileInfo.Name,
            DirectoryName = fileInfo.DirectoryName,
            FileSize = fileInfo.Length,
            CreationTime = fileInfo.CreationTime,
            LastModifiedTime = fileInfo.LastWriteTime,
            Supported = true
        };

        // Try to extract text content
        try
        {
            var extractedText = await _contentExtractor.ExtractTextAsync(filePath, cancellationToken);
            result.ExtractedText = extractedText;

            if (!string.IsNullOrEmpty(extractedText))
            {
                // Classify the extracted text
                var topics = await _contentClassifier.ClassifyAsync(
                    extractedText, fileInfo.Name, cancellationToken);
                foreach (var topic in topics)
                {
                    // Topics will be added to the folder analysis separately
                }
            }
        }
        catch (Exception ex)
        {
            result.ErrorMessage = $"Failed to extract content: {ex.Message}";
        }

        return result;
    }

    /// <summary>
    /// Gets the supported extension for a file type.
    /// </summary>
    private static string? GetSupportedExtension(string extension)
    {
        var supportedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            // Documents
            ".pdf", ".doc", ".docx", ".txt", ".md", ".rtf",

            // Data
            ".csv", ".json", ".xml",

            // Source Code
            ".js", ".ts", ".jsx", ".tsx", ".cs", ".java", ".py", ".go", ".rs",
            ".cpp", ".h", ".sql", ".html", ".css", ".scss",

            // Images
            ".jpg", ".jpeg", ".png", ".webp", ".gif", ".bmp"
        };

        if (supportedExtensions.Contains(extension))
            return extension;

        return null;
    }

    /// <summary>
    /// Extracts keywords from a file name.
    /// </summary>
    private static List<string> ExtractKeywordsFromName(string fileName)
    {
        var keywords = new List<string>();
        var nameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);

        // Common prefixes/suffixes to ignore
        var ignoreWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "img", "photo", "picture", "scan", "copy", "backup", "new", "old",
            "file", "document", "data", "temp", "backup", "archive"
        };

        var parts = nameWithoutExtension.Split(new[] { '_', '-', ' ' }, StringSplitOptions.RemoveEmptyEntries);

        foreach (var part in parts)
        {
            var trimmed = part.Trim();
            if (trimmed.Length > 2 && !ignoreWords.Contains(trimmed))
            {
                keywords.Add(trimmed);
            }
        }

        return keywords;
    }

    /// <summary>
    /// Generates a reason string explaining the folder name suggestion.
    /// </summary>
    private static string GenerateReason(FolderAnalysisData analysis)
    {
        if (analysis.Files.Count == 0)
            return "No files found.";

        var reasons = new List<string>();

        if (analysis.Keywords.Count > 0)
        {
            var topKeywords = analysis.Keywords.Distinct(StringComparer.OrdinalIgnoreCase).Take(3).ToList();
            reasons.Add($"File names mention: {string.Join(", ", topKeywords)}");
        }

        if (analysis.DetectedTopics.Count > 0)
        {
            var topTopics = analysis.DetectedTopics.Take(3).ToList();
            reasons.Add($"Detected topics: {string.Join(", ", topTopics)}");
        }

        if (analysis.ExtensionCounts.Count > 0)
        {
            var topExt = analysis.ExtensionCounts.OrderByDescending(k => k.Value).First();
            reasons.Add($"{topExt.Value} .{topExt.Key} file(s)");
        }

        reasons.Add(analysis.Files.Count == 1 ? "1 file total" : $"{analysis.Files.Count} files total");

        return reasons.Count > 0 ? string.Join(". ", reasons) : "Folder analyzed";
    }

    /// <summary>
    /// Generates alternative folder name suggestions.
    /// </summary>
    private static List<string> GenerateAlternativeSuggestions(FolderAnalysisData analysis)
    {
        var alternatives = new List<string>();

        if (analysis.Files.Count == 0)
            return alternatives;

        // Generate alternatives based on different keyword sets
        var allKeywords = new HashSet<string>(analysis.Keywords, StringComparer.OrdinalIgnoreCase);

        // Try different category combinations
        if (allKeywords.Contains("invoice") || allKeywords.Contains("receipt"))
        {
            alternatives.Add("Financial Documents");
            alternatives.Add("Invoices & Receipts");
        }

        if (allKeywords.Contains("react") || allKeywords.Contains("javascript") || allKeywords.Contains("node"))
        {
            alternatives.Add("Web Development");
            alternatives.Add("Software Development");
        }

        if (analysis.DetectedTopics.Count > 0)
        {
            alternatives.AddRange(analysis.DetectedTopics.Take(2));
        }

        // Add generic alternatives based on file count
        alternatives.Add($"Folder ({analysis.Files.Count})");
        alternatives.Add($"Documents ({analysis.Files.Count})");

        return alternatives.Distinct().Take(5).ToList();
    }
}