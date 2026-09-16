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

        // Score each trigger keyword by how many files mention it; the best
        // trigger's primary suggestion becomes the folder name.
        var triggerHits = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in analysis.Files)
        {
            // Match against the stem only so extension triggers ("pdf", "docx")
            // never outrank semantic triggers ("invoice", "react").
            var haystack = Path.GetFileNameWithoutExtension(file.FileName ?? string.Empty).ToLowerInvariant();
            foreach (var trigger in KeywordCategories.Keys)
            {
                if (haystack.Contains(trigger.ToLowerInvariant()))
                    triggerHits[trigger] = triggerHits.TryGetValue(trigger, out var n) ? n + 1 : 1;
            }
        }

        if (triggerHits.Count > 0)
        {
            var best = triggerHits.OrderByDescending(kvp => kvp.Value).First();
            // Require the trigger in a meaningful share of files (>=25%, at least 1 of few).
            var share = (double)best.Value / Math.Max(1, analysis.Files.Count);
            if (share >= 0.25)
                return KeywordCategories[best.Key].First();
        }

        // Fallback: generate a name based on file types and count
        var extension = analysis.Files.Count > 0 ? Path.GetExtension(analysis.Files[0].FileName ?? "") : "";
        var fileCount = analysis.Files.Count;

        if (!string.IsNullOrEmpty(extension))
        {
            var extWithoutDot = extension.TrimStart('.');
            return $"{fileCount} {extWithoutDot.ToUpper()} Files";
        }

        return $"Folder ({fileCount})";
    }
}