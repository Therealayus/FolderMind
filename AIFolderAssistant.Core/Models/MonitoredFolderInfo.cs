namespace AIFolderAssistant.Core.Models;

/// <summary>
/// UI/domain-friendly view of a monitored folder.
/// </summary>
public sealed class MonitoredFolderInfo
{
    public string Path { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public string Status => IsEnabled ? "Monitoring" : "Paused";
    public DateTime LastScannedAt { get; set; }
    public string LastScannedDisplay => LastScannedAt == default
        ? "Never"
        : LastScannedAt.ToLocalTime().ToString("g");
}

/// <summary>
/// UI-friendly view of a suggestion.
/// </summary>
public sealed class SuggestionInfo
{
    public int Id { get; set; }
    public string FolderPath { get; set; } = string.Empty;
    public string OriginalName { get; set; } = string.Empty;
    public string SuggestedName { get; set; } = string.Empty;
    public double Confidence { get; set; }
    public string ConfidenceBand => Confidence >= 0.9 ? "High" : Confidence >= 0.8 ? "Medium" : "Low";
    public string? Reason { get; set; }
    public string Status { get; set; } = "Pending";
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// UI-friendly view of a history entry.
/// </summary>
public sealed class HistoryEntry
{
    public string FolderPath { get; set; } = string.Empty;
    public string OriginalName { get; set; } = string.Empty;
    public string NewName { get; set; } = string.Empty;
    public bool Success { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool CanUndo { get; set; }
}
