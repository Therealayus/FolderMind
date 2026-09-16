using AIFolderAssistant.Core.Analysis;

namespace AIFolderAssistant.Core.Engines;

/// <summary>
/// Deterministic local content classifier built on a configurable
/// keyword → category map. No network, no model downloads, fully offline.
/// </summary>
public sealed class KeywordContentClassifier : IContentClassifier
{
    private readonly Dictionary<string, string[]> _keywordToCategories;

    public KeywordContentClassifier() : this(DefaultMap()) { }

    public KeywordContentClassifier(Dictionary<string, string[]> keywordToCategories)
    {
        _keywordToCategories = keywordToCategories;
    }

    public static Dictionary<string, string[]> DefaultMap() => new(StringComparer.OrdinalIgnoreCase)
    {
        ["invoice"] = new[] { "Financial" },
        ["receipt"] = new[] { "Financial" },
        ["payment"] = new[] { "Financial" },
        ["gst"] = new[] { "Financial", "Tax" },
        ["tax"] = new[] { "Financial", "Tax" },
        ["bank"] = new[] { "Financial" },
        ["salary"] = new[] { "Financial" },
        ["budget"] = new[] { "Financial" },
        ["react"] = new[] { "Web Development" },
        ["angular"] = new[] { "Web Development" },
        ["vue"] = new[] { "Web Development" },
        ["javascript"] = new[] { "Web Development" },
        ["typescript"] = new[] { "Web Development" },
        ["node"] = new[] { "Web Development", "Backend" },
        ["python"] = new[] { "Software Development", "Data" },
        ["pandas"] = new[] { "Data" },
        ["dataset"] = new[] { "Data" },
        ["report"] = new[] { "Documents", "Work" },
        ["resume"] = new[] { "Career", "Documents" },
        ["cv"] = new[] { "Career", "Documents" },
        ["contract"] = new[] { "Legal", "Documents" },
        ["agreement"] = new[] { "Legal", "Documents" },
        ["trip"] = new[] { "Travel" },
        ["vacation"] = new[] { "Travel" },
        ["goa"] = new[] { "Travel" },
        ["hotel"] = new[] { "Travel" },
        ["flight"] = new[] { "Travel" },
        ["wedding"] = new[] { "Events", "Photos" },
        ["birthday"] = new[] { "Events", "Photos" },
        ["design"] = new[] { "Design" },
        ["mockup"] = new[] { "Design" },
        ["system"] = new[] { "Software Development" },
        ["architecture"] = new[] { "Software Development" },
        ["tutorial"] = new[] { "Learning" },
        ["course"] = new[] { "Learning" },
        ["lecture"] = new[] { "Learning" },
    };

    public Task<List<ClassifiedTopic>> ClassifyAsync(string text, string fileName, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var haystack = $"{fileName} {text}".ToLowerInvariant();
        var scores = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var (keyword, categories) in _keywordToCategories)
        {
            if (haystack.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                foreach (var category in categories)
                    scores[category] = scores.TryGetValue(category, out var n) ? n + 1 : 1;
            }
        }

        var total = Math.Max(1, scores.Values.Sum());
        var topics = scores
            .OrderByDescending(kvp => kvp.Value)
            .Take(3)
            .Select(kvp => new ClassifiedTopic
            {
                Topic = kvp.Key,
                Confidence = Math.Round((double)kvp.Value / total, 2)
            })
            .ToList();
        return Task.FromResult(topics);
    }
}
