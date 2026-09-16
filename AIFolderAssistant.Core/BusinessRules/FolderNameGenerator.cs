using AIFolderAssistant.Core.Analysis;

namespace AIFolderAssistant.Core.BusinessRules;

/// <summary>
/// Generates folder name suggestions based on analysis data.
/// Uses keyword clustering, topic detection, and file type patterns.
/// </summary>
public class FolderNameGenerator : IFolderNameGenerator
{
    private static readonly Dictionary<string, List<string>> KeywordCategories = new()
    {
        // Financial
        ["invoice"] = new List<string> { "Financial Documents", "Invoices & Receipts", "Payment Records" },
        ["receipt"] = new List<string> { "Financial Documents", "Invoices & Receipts", "Payment Records" },
        ["tax"] = new List<string> { "Tax Records", "Financial Documents", "Tax Documentation" },
        ["payment"] = new List<string> { "Payment Records", "Financial Documents", "Bank Statements" },
        ["bank"] = new List<string> { "Bank Records", "Financial Documents", "Account Statements" },

        // Development/Tech
        ["react"] = new List<string> { "Software Development", "Web Development", "JavaScript Projects" },
        ["node"] = new List<string> { "Node.js Development", "Server-Side Development", "JavaScript Projects" },
        ["typescript"] = new List<string> { "TypeScript Projects", "Software Development", "Frontend Development" },
        ["javascript"] = new List<string> { "JavaScript Projects", "Web Development", "Software Development" },
        ["cs"] = new List<string> { "C# Projects", ".NET Development", "Software Development" },
        ["java"] = new List<string> { "Java Projects", "Enterprise Development", "Software Development" },
        ["python"] = new List<string> { "Python Projects", "Data Science", "Software Development" },
        ["go"] = new List<string> { "Go Projects", "Golang Development", "Systems Programming" },
        ["cpp"] = new List<string> { "C++ Projects", "Systems Development", "Performance Coding" },

        // Documents
        ["gst"] = new List<string> { "Tax Records", "Financial Documents", "Compliance Documents" },
        ["pdf"] = new List<string> { "Documents", "File Repository", "Digital Documents" },
        ["docx"] = new List<string> { "Documents", "Word Documents", "Office Files" },
        ["project"] = new List<string> { "Project Files", "Working Directory", "Project Folder" },

        // Trip/Personal
        ["trip"] = new List<string> { "Trip Photos", "Memories", "Travel Documentation" },
        ["goa"] = new List<string> { "Goa Trip", "Travel Photos", "Vacation Memories" },
        ["vacation"] = new List<string> { "Trip Photos", "Memories", "Travel Documentation" },

        // General
        ["photos"] = new List<string> { "Photo Collection", "Images", "Memory Folder" },
        ["images"] = new List<string> { "Image Gallery", "Photos", "Picture Folder" },
    };

    private static readonly Dictionary<string, string> TriggerCategories = new(StringComparer.OrdinalIgnoreCase)
    {
        ["invoice"] = "Financial", ["receipt"] = "Financial", ["tax"] = "Financial",
        ["payment"] = "Financial", ["bank"] = "Financial", ["bill"] = "Financial",
        ["react"] = "Web Development", ["node"] = "Web Development",
        ["typescript"] = "Web Development", ["javascript"] = "Web Development",
        ["cs"] = "Software Development", ["java"] = "Software Development",
        ["python"] = "Software Development", ["go"] = "Software Development",
        ["cpp"] = "Software Development",
        ["gst"] = "Financial", ["pdf"] = "Documents", ["docx"] = "Documents",
        ["project"] = "Projects",
        ["trip"] = "Travel", ["goa"] = "Travel", ["vacation"] = "Travel",
        ["photos"] = "Photos", ["images"] = "Photos",
    };

    private static readonly Dictionary<string, string> CategoryPrimaryName = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Financial"] = "Financial Documents",
        ["Web Development"] = "Web Development",
        ["Software Development"] = "Software Development",
        ["Documents"] = "Documents",
        ["Projects"] = "Project Files",
        ["Travel"] = "Trip Photos",
        ["Photos"] = "Photo Collection",
    };

    private static string TriggerCategory(string trigger) =>
        TriggerCategories.TryGetValue(trigger, out var category) ? category : "General";

    private static string? TopicCategory(string topic)
    {
        foreach (var (category, primary) in CategoryPrimaryName)
        {
            if (topic.Equals(category, StringComparison.OrdinalIgnoreCase) ||
                topic.Equals(primary, StringComparison.OrdinalIgnoreCase))
                return category;
        }
        return null;
    }

    /// <summary>
    /// Generates a folder name suggestion based on analysis data.
    /// </summary>
    /// <param name="analysis">The folder analysis data.</param>
    /// <returns>The suggested folder name.</returns>
    public string GenerateName(FolderAnalysisData analysis)
    {
        if (analysis.Files.Count == 0)
            return "Unnamed Folder";

        // Collect all keywords from file names and extensions
        var allKeywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var file in analysis.Files)
        {
            if (!string.IsNullOrEmpty(file.FileName))
            {
                var filenameLower = file.FileName.ToLowerInvariant();
                foreach (var keyword in KeywordCategories.Keys)
                {
                    if (filenameLower.Contains(keyword.ToLowerInvariant()))
                    {
                        allKeywords.Add(keyword);
                    }
                }
            }

            if (analysis.ExtensionCounts.TryGetValue(file?.Extension ?? "", out var count))
            {
                // Track extension types
            }
        }

        // Also check detected topics
        foreach (var topic in analysis.DetectedTopics)
        {
            allKeywords.Add(topic);
        }

        // Aggregate trigger hits per CATEGORY (invoice + receipt + gst all feed
        // "Financial") so mixed-but-coherent folders still resolve. Match against
        // the stem only so extension triggers ("pdf") never outrank semantics.
        var categoryHits = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in analysis.Files)
        {
            var haystack = Path.GetFileNameWithoutExtension(file.FileName ?? string.Empty).ToLowerInvariant();
            var fileCategories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var trigger in KeywordCategories.Keys)
            {
                if (haystack.Contains(trigger.ToLowerInvariant(), StringComparison.Ordinal))
                    fileCategories.Add(TriggerCategory(trigger));
            }
            foreach (var category in fileCategories)
                categoryHits[category] = categoryHits.TryGetValue(category, out var n) ? n + 1 : 1;
        }

        // Detected topics vote for their category too.
        foreach (var topic in analysis.DetectedTopics)
        {
            var category = TopicCategory(topic);
            if (category is not null)
                categoryHits[category] = categoryHits.TryGetValue(category, out var n) ? n + 1 : 1;
        }

        if (categoryHits.Count > 0)
        {
            var best = categoryHits.OrderByDescending(kvp => kvp.Value).First();
            // Require the winning category in a meaningful share of files (>=25%).
            var share = (double)best.Value / Math.Max(1, analysis.Files.Count);
            if (share >= 0.25 && CategoryPrimaryName.TryGetValue(best.Key, out var primary))
                return primary;
        }

        // Fallback: dominant extension + count (consistent with the reason text).
        var fileCount = analysis.Files.Count;
        if (analysis.ExtensionCounts.Count > 0)
        {
            var topExt = analysis.ExtensionCounts.OrderByDescending(kvp => kvp.Value).First().Key;
            return $"{fileCount} {topExt.ToUpperInvariant()} Files";
        }

        return $"Folder ({fileCount})";
    }
}