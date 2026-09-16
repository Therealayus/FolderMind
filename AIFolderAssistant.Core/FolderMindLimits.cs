namespace AIFolderAssistant.Core;

/// <summary>
/// Central safety limits for folder analysis. Large folders are never loaded
/// wholesale: discovery is bounded, analysis works on an even stride-sample,
/// and per-file reads are capped (see content extractors).
/// </summary>
public static class FolderMindLimits
{
    /// <summary>Default maximum files analyzed per folder.</summary>
    public const int DefaultMaxFiles = 500;

    /// <summary>Bounds for the user-configurable per-folder cap.</summary>
    public const int MinMaxFiles = 50;
    public const int AbsoluteMaxFiles = 2000;

    /// <summary>
    /// Hard bound on paths materialized during discovery. Even a million-file
    /// tree costs only a few MB of path strings; analysis still samples down
    /// to <see cref="DefaultMaxFiles"/>.
    /// </summary>
    public const int MaxDiscoveredPaths = 20_000;

    public static int ClampMaxFiles(int value) =>
        Math.Clamp(value <= 0 ? DefaultMaxFiles : value, MinMaxFiles, AbsoluteMaxFiles);
}
