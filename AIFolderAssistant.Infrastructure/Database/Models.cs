namespace AIFolderAssistant.Infrastructure.Database;

/// <summary>
/// Represents a folder being monitored by FolderMind AI.
/// </summary>
public sealed class MonitoredFolder
{
    public int Id { get; set; }
    public string Path { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime LastScannedAt { get; set; }
}

/// <summary>
/// Represents an analysis run over a folder's contents.
/// </summary>
public sealed class FolderAnalysis
{
    public int Id { get; set; }
    public int MonitoredFolderId { get; set; }
    public string FolderPath { get; set; } = string.Empty;
    public string OriginalName { get; set; } = string.Empty;
    public string? SuggestedName { get; set; }
    public double Confidence { get; set; }
    public string? Reason { get; set; }
    public int FileCount { get; set; }
    public int ProcessedFileCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
}

/// <summary>
/// Status lifecycle of a suggestion: pending review, accepted (renamed), or ignored.
/// </summary>
public enum SuggestionStatus
{
    Pending = 0,
    Accepted = 1,
    Ignored = 2
}

/// <summary>
/// A folder-name suggestion surfaced to the user.
/// </summary>
public sealed class Suggestion
{
    public int Id { get; set; }
    public string FolderPath { get; set; } = string.Empty;
    public string OriginalName { get; set; } = string.Empty;
    public string SuggestedName { get; set; } = string.Empty;
    public double Confidence { get; set; }
    public string? Reason { get; set; }
    public SuggestionStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
}

/// <summary>
/// History record of a folder rename operation.
/// </summary>
public sealed class RenameHistory
{
    public int Id { get; set; }
    public string FolderPath { get; set; } = string.Empty;
    public string OriginalName { get; set; } = string.Empty;
    public string NewName { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Application setting stored as key/value.
/// </summary>
public sealed class Setting
{
    public int Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public DateTime LastModified { get; set; }
}

/// <summary>
/// Aggregated AI usage tracking record.
/// </summary>
public sealed class AIUsage
{
    public int Id { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public int RequestCount { get; set; }
    public double TotalTokensUsed { get; set; }
    public DateTime LastRequest { get; set; }
}

/// <summary>
/// Diagnostic log entry persisted locally.
/// </summary>
public sealed class Diagnostic
{
    public int Id { get; set; }
    public string Level { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? Details { get; set; }
    public DateTime Timestamp { get; set; }
}
