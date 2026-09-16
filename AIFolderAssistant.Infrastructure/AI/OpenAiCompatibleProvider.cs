using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using AIFolderAssistant.Core.AI;
using AIFolderAssistant.Core.Analysis;
using AIFolderAssistant.Core.BusinessRules;
using Microsoft.Extensions.Logging;

namespace AIFolderAssistant.Infrastructure.AI;

/// <summary>
/// OpenAI-compatible chat-completions provider (works with OpenAI, Azure OpenAI,
/// and any OpenAI-compatible local endpoint). Sends only a structured summary —
/// never full document contents — and validates/sanitizes every response.
/// Throws on failure so callers can fall back to local suggestions; never crashes the app.
/// </summary>
public sealed class OpenAiCompatibleProvider : IAIProvider
{
    private readonly HttpClient _http;
    private readonly ILogger<OpenAiCompatibleProvider> _logger;
    private readonly string _endpoint;
    private readonly string _model;
    private readonly Func<string?> _apiKeyProvider;

    public OpenAiCompatibleProvider(
        HttpClient http,
        ILogger<OpenAiCompatibleProvider> logger,
        string endpoint,
        string model,
        Func<string?> apiKeyProvider)
    {
        _http = http;
        _logger = logger;
        _endpoint = endpoint.TrimEnd('/');
        _model = model;
        _apiKeyProvider = apiKeyProvider;
    }

    public async Task<FolderNameSuggestion> GenerateFolderNameAsync(FolderAnalysisData analysis, CancellationToken cancellationToken)
    {
        var apiKey = _apiKeyProvider();
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("Cloud AI is enabled but no API key is configured.");
        if (string.IsNullOrWhiteSpace(_endpoint))
            throw new InvalidOperationException("Cloud AI endpoint is not configured.");

        var summary = BuildSummary(analysis);
        var prompt = BuildPrompt(summary);

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{_endpoint}/chat/completions");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        var payload = new
        {
            model = _model,
            temperature = 0.2,
            max_tokens = 300,
            response_format = new { type = "json_object" },
            messages = new object[]
            {
                new { role = "system", content = "You suggest concise Windows folder names. Reply with JSON only: {\"suggestion\": string, \"alternatives\": string[], \"confidence\": number, \"reason\": string}." },
                new { role = "user", content = prompt }
            }
        };
        request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("AI provider returned {Status}: {Body}", (int)response.StatusCode, Truncate(body, 300));
            throw new InvalidOperationException($"AI provider returned HTTP {(int)response.StatusCode}.");
        }

        return ParseAndValidate(body);
    }

    internal static object BuildSummary(FolderAnalysisData analysis) => new
    {
        fileCount = analysis.Files.Count,
        extensions = analysis.ExtensionCounts.OrderByDescending(kvp => kvp.Value).Take(8).Select(kvp => "." + kvp.Key).ToArray(),
        topics = analysis.DetectedTopics.Take(8).ToArray(),
        sampleFileNames = analysis.Files.Take(12).Select(f => f.FileName ?? string.Empty).Where(n => n.Length > 0).ToArray()
    };

    private static string BuildPrompt(object summary) =>
        "Given this folder summary, suggest a concise (1-5 words) human-readable folder name:\n" +
        JsonSerializer.Serialize(summary);

    private FolderNameSuggestion ParseAndValidate(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            if (!root.TryGetProperty("choices", out var choices) || choices.GetArrayLength() == 0)
                throw new InvalidOperationException("AI response contained no choices.");
            var content = choices[0].GetProperty("message").GetProperty("content").GetString();
            if (string.IsNullOrWhiteSpace(content))
                throw new InvalidOperationException("AI response content was empty.");

            using var inner = JsonDocument.Parse(content);
            var obj = inner.RootElement;
            var suggestion = NameSanitizer.Sanitize(obj.TryGetProperty("suggestion", out var s) ? s.GetString() : null);
            var alternatives = obj.TryGetProperty("alternatives", out var alts) && alts.ValueKind == JsonValueKind.Array
                ? alts.EnumerateArray().Select(e => NameSanitizer.Sanitize(e.GetString())).Where(n => n.Length > 0).Distinct().Take(3).ToList()
                : new List<string>();
            var confidence = obj.TryGetProperty("confidence", out var conf) && conf.TryGetDouble(out var c)
                ? Math.Clamp(c, 0.0, 1.0)
                : 0.5;
            var reason = obj.TryGetProperty("reason", out var r) ? (r.GetString() ?? string.Empty) : string.Empty;

            return new FolderNameSuggestion
            {
                Suggestion = suggestion,
                Alternatives = alternatives,
                Confidence = Math.Round(confidence, 2),
                Reason = reason.Length > 500 ? reason.Substring(0, 500) : reason
            };
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse AI response; falling back to local suggestions.");
            throw new InvalidOperationException("AI response was not valid JSON.", ex);
        }
    }

    private static string Truncate(string s, int max) => s.Length <= max ? s : s.Substring(0, max) + "…";
}
