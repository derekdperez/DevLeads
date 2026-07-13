using System.Text.Json;
using Microsoft.Extensions.Logging;
using DevLeads.Core.Connectors;
using DevLeads.Core.Entities;

namespace DevLeads.Infrastructure.Connectors;

/// <summary>Discovers founder/operator pain via the Hacker News (Algolia) search API.</summary>
public sealed class HackerNewsConnector : ISourceConnector
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly ILogger<HackerNewsConnector> _log;

    public HackerNewsConnector(IHttpClientFactory httpFactory, ILogger<HackerNewsConnector> log)
    {
        _httpFactory = httpFactory;
        _log = log;
    }

    public string SourceKey => "hackernews";
    public string DisplayName => "Hacker News Search";

    public async Task<IReadOnlyList<RawSourceItem>> FetchAsync(SourceConnectorConfig config, CancellationToken ct)
    {
        var http = _httpFactory.CreateClient(ConnectorSupport.HttpClientName);
        var items = new Dictionary<string, RawSourceItem>();

        var daysBack = GetInt(config, "daysBack", 7);
        var since = (config.Since ?? DateTimeOffset.UtcNow.AddDays(-daysBack)).ToUnixTimeSeconds();
        foreach (var term in PickSearchTerms(config.Terms, 8))
        {
            ct.ThrowIfCancellationRequested();
            var url = $"https://hn.algolia.com/api/v1/search_by_date?query={Uri.EscapeDataString(term)}&tags=story&numericFilters=created_at_i>{since}&hitsPerPage=15";
            try
            {
                using var doc = JsonDocument.Parse(await http.GetStringAsync(url, ct));
                foreach (var hit in doc.RootElement.GetProperty("hits").EnumerateArray())
                {
                    var id = hit.GetProperty("objectID").GetString() ?? "";
                    if (id.Length == 0 || items.ContainsKey(id)) continue;
                    var title = hit.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "";
                    if (string.IsNullOrWhiteSpace(title)) continue;
                    var body = hit.TryGetProperty("story_text", out var st) ? st.GetString() ?? "" : "";
                    var author = hit.TryGetProperty("author", out var a) ? a.GetString() : null;
                    var storyUrl = hit.TryGetProperty("url", out var u) && u.ValueKind == JsonValueKind.String
                        ? u.GetString()! : $"https://news.ycombinator.com/item?id={id}";
                    var posted = hit.TryGetProperty("created_at", out var c) && c.ValueKind == JsonValueKind.String
                        ? DateTimeOffset.Parse(c.GetString()!) : DateTimeOffset.UtcNow;

                    items[id] = ConnectorSupport.NewItem(config.SourceKey, id, title, body, storyUrl, author,
                        author is null ? null : $"https://news.ycombinator.com/user?id={author}", posted, hit.GetRawText());
                    if (items.Count >= config.MaxItems) break;
                }
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) { _log.LogWarning(ex, "HN fetch failed for term {Term}", term); }

            if (items.Count >= config.MaxItems) break;
        }

        return items.Values.Take(config.MaxItems).ToList();
    }

    // Algolia matches all query words, so long pack phrases ("customers cannot check
    // out") return nothing. Short phrases (1-3 words) are what actually hit; prefer the
    // 2-3-word ones — single words are too generic on their own but acceptable as filler.
    private static List<string> PickSearchTerms(IEnumerable<string> terms, int max) =>
        terms.Where(t => !string.IsNullOrWhiteSpace(t))
            .Select(t => t.Trim())
            .Where(t => t.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length <= 3)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(t => t.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length is 2 or 3)
            .ThenBy(t => t.Length)
            .Take(max)
            .ToList();

    private static int GetInt(SourceConnectorConfig config, string key, int fallback) =>
        config.Parameters.TryGetValue(key, out var raw) && int.TryParse(raw, out var value) ? value : fallback;

    public async Task<ConnectorHealth> CheckHealthAsync(CancellationToken ct)
    {
        try
        {
            var http = _httpFactory.CreateClient(ConnectorSupport.HttpClientName);
            var resp = await http.GetAsync("https://hn.algolia.com/api/v1/search?tags=front_page&hitsPerPage=1", ct);
            return new ConnectorHealth { Healthy = resp.IsSuccessStatusCode, Message = resp.IsSuccessStatusCode ? "OK" : $"HTTP {(int)resp.StatusCode}" };
        }
        catch (Exception ex) { return new ConnectorHealth { Healthy = false, Message = ex.Message }; }
    }
}
