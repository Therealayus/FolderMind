using AIFolderAssistant.Core.Analysis;

namespace AIFolderAssistant.Core.Engines;

/// <summary>
/// Default <see cref="IFolderAnalysisPipeline"/> implementation.
/// Currently delegates to the local-first <see cref="IFolderAnalyzer"/>.
/// Extension point: insert AI enhancement, caching (content fingerprints),
/// and sampling limits here without changing callers.
/// </summary>
public sealed class LocalAnalysisPipeline : IFolderAnalysisPipeline
{
    private readonly IFolderAnalyzer _analyzer;

    public LocalAnalysisPipeline(IFolderAnalyzer analyzer)
    {
        _analyzer = analyzer;
    }

    public Task<FolderAnalysisResult> RunPipelineAsync(string folderPath, CancellationToken cancellationToken) =>
        _analyzer.AnalyzeAsync(folderPath, cancellationToken);
}
