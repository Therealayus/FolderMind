using AIFolderAssistant.Core.Analysis;
using AIFolderAssistant.Core.Interfaces;

namespace AIFolderAssistant.Core.Engines;

/// <summary>
/// Default <see cref="IFolderAnalysisPipeline"/> implementation.
/// Threads user settings (per-folder file cap) into the local-first analyzer.
/// Extension point: AI enhancement, fingerprint caching, and sampling policy
/// plug in here without changing callers.
/// </summary>
public sealed class LocalAnalysisPipeline : IFolderAnalysisPipeline
{
    private readonly IFolderAnalyzer _analyzer;
    private readonly ISettingsService _settings;

    public LocalAnalysisPipeline(IFolderAnalyzer analyzer, ISettingsService settings)
    {
        _analyzer = analyzer;
        _settings = settings;
    }

    public Task<FolderAnalysisResult> RunPipelineAsync(string folderPath, CancellationToken cancellationToken)
    {
        int maxFiles;
        try
        {
            _settings.Load();
            maxFiles = FolderMindLimits.ClampMaxFiles(_settings.Current.Monitoring.MaxFilesPerFolder);
        }
        catch
        {
            maxFiles = FolderMindLimits.DefaultMaxFiles;
        }
        return _analyzer.AnalyzeAsync(folderPath, cancellationToken, maxFiles);
    }
}
