using System.Text.Json;
using Microsoft.Extensions.Logging;
using DevLeads.Core.Connectors;
using DevLeads.Core.Entities;

namespace DevLeads.Infrastructure.Connectors;

/// <summary>
/// Searches public GitHub issues for money-attached work: bounty-platform issues
/// (BountyHub, Algora, IssueHunt and friends all anchor their bounties to GitHub issues)
/// and feature requests where the poster says they'd pay. Uses the public search API —
/// unauthenticated it allows ~10 searches/minute for the whole IP, so runs are paced and
/// query counts kept small; set the GITHUB_TOKEN environment variable to raise the limit.
/// </summary>
public sealed class GitHubSearchConnector : ISourceConnector
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly ILogger<GitHubSearchConnector> _log;

    public GitHubSearchConnector(IHttpClientFactory httpFactory, ILogger<GitHubSearchConnector> log)
    {
        _httpFactory = httpFactory;
        _log = log;
    }

    public string SourceKey => "github_search";
    public string DisplayName => "GitHub issue search";

    public async Task<IReadOnlyList<RawSourceItem>> FetchAsync(SourceConnectorConfig config, CancellationToken ct)
    {
        var queries = (config.Parameters.TryGetValue("queries", out var q) ? q : "label:bounty")
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var daysBack = GetInt(config, "daysBack", 30);
        var requireSkillMatch = config.Parameters.TryGetValue("requireSkillMatch", out var rsm) &&
                                rsm.Equals("true", StringComparison.OrdinalIgnoreCase);

        var http = CreateClient();
        var items = new Dictionary<string, RawSourceItem>();
        var since = DateTimeOffset.UtcNow.AddDays(-daysBack).ToString("yyyy-MM-dd");
        var first = true;

        foreach (var raw in queries.Take(6)) // stay well inside the search rate limit
        {
            ct.ThrowIfCancellationRequested();
            if (!first) await Task.Delay(TimeSpan.FromSeconds(7), ct); // ~10 searches/min unauthenticated
            first = false;

            // A leading '*' marks a query that already encodes fit (language:C# etc.) or a
            // tiny-volume platform net — its results skip the per-item skill-text filter,
            // which bounty issues rarely satisfy (their text seldom names the language).
            var skipSkillFilter = raw.StartsWith('*');
            var query = skipSkillFilter ? raw[1..].Trim() : raw;

            var full = $"{query} is:issue is:open created:>={since}";
            var url = $"https://api.github.com/search/issues?q={Uri.EscapeDataString(full)}&sort=created&order=desc&per_page=30";
            try
            {
                using var resp = await http.GetAsync(url, ct);
                var json = await resp.Content.ReadAsStringAsync(ct);
                if ((int)resp.StatusCode is 403 or 429)
                {
                    _log.LogWarning("GitHub search rate-limited (HTTP {Status}) — aborting run.", (int)resp.StatusCode);
                    break;
                }
                if (!resp.IsSuccessStatusCode)
                {
                    _log.LogWarning("GitHub search failed (HTTP {Status}) for query {Query}", (int)resp.StatusCode, query);
                    continue;
                }

                using var doc = JsonDocument.Parse(json);
                if (!doc.RootElement.TryGetProperty("items", out var arr)) continue;
                foreach (var issue in arr.EnumerateArray())
                {
                    var item = ParseIssue(issue, config, requireSkillMatch && !skipSkillFilter);
                    if (item is not null && !items.ContainsKey(item.ExternalId))
                    {
                        items[item.ExternalId] = item;
                        if (items.Count >= config.MaxItems) break;
                    }
                }
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) { _log.LogWarning(ex, "GitHub search failed for query {Query}", query); }

            if (items.Count >= config.MaxItems) break;
        }

        return items.Values.Take(config.MaxItems).ToList();
    }

    private static RawSourceItem? ParseIssue(JsonElement issue, SourceConnectorConfig config, bool requireSkillMatch)
    {
        var id = issue.TryGetProperty("id", out var idEl) ? idEl.GetInt64().ToString() : "";
        var title = issue.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "";
        var htmlUrl = issue.TryGetProperty("html_url", out var u) ? u.GetString() ?? "" : "";
        if (id.Length == 0 || title.Length == 0 || htmlUrl.Length == 0) return null;

        var body = issue.TryGetProperty("body", out var b) && b.ValueKind == JsonValueKind.String
            ? b.GetString() ?? "" : "";
        if (body.Length > 4000) body = body[..4000];

        var labels = issue.TryGetProperty("labels", out var labelArr) && labelArr.ValueKind == JsonValueKind.Array
            ? labelArr.EnumerateArray()
                .Select(l => l.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "")
                .Where(s => s.Length > 0).ToList()
            : new List<string>();

        string? author = null, authorUrl = null;
        if (issue.TryGetProperty("user", out var user) && user.ValueKind == JsonValueKind.Object)
        {
            author = user.TryGetProperty("login", out var login) ? login.GetString() : null;
            authorUrl = user.TryGetProperty("html_url", out var uu) ? uu.GetString() : null;
        }

        if (requireSkillMatch && config.SkillTerms.Count > 0)
        {
            // URL excluded on purpose: "github.com/..." would match tool-name skills.
            var matchText = $"{title}\n{body}\n{string.Join(' ', labels)}";
            if (!config.SkillTerms.Any(s => matchText.Contains(s, StringComparison.OrdinalIgnoreCase)))
                return null;
        }

        var posted = issue.TryGetProperty("created_at", out var c) && c.ValueKind == JsonValueKind.String
            ? DateTimeOffset.Parse(c.GetString()!) : DateTimeOffset.UtcNow;

        // Competition facts the search result already carries: an assigned issue is
        // claimed work, and a comment pile means other contributors are on it. Surface
        // them in the item text so triage and scoring can see them without extra calls.
        var assigneeCount = issue.TryGetProperty("assignees", out var assignees) && assignees.ValueKind == JsonValueKind.Array
            ? assignees.GetArrayLength()
            : issue.TryGetProperty("assignee", out var a) && a.ValueKind == JsonValueKind.Object ? 1 : 0;
        var commentCount = issue.TryGetProperty("comments", out var cc) && cc.ValueKind == JsonValueKind.Number
            ? cc.GetInt32() : 0;

        var summary = labels.Count > 0 ? $"Labels: {string.Join(", ", labels)}.\n\n{body}" : body;
        summary += $"\n\n[meta] assignees:{assigneeCount} comments:{commentCount}";
        return ConnectorSupport.NewItem(config.SourceKey, id, title, summary, htmlUrl, author, authorUrl,
            posted, issue.GetRawText());
    }

    private HttpClient CreateClient()
    {
        var http = _httpFactory.CreateClient(ConnectorSupport.HttpClientName);
        http.DefaultRequestHeaders.Accept.Clear();
        http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        var token = Environment.GetEnvironmentVariable("GITHUB_TOKEN");
        if (!string.IsNullOrWhiteSpace(token))
            http.DefaultRequestHeaders.Authorization = new("Bearer", token);
        return http;
    }

    private static int GetInt(SourceConnectorConfig config, string key, int fallback) =>
        config.Parameters.TryGetValue(key, out var raw) && int.TryParse(raw, out var value) ? value : fallback;

    public async Task<ConnectorHealth> CheckHealthAsync(CancellationToken ct)
    {
        try
        {
            var http = CreateClient();
            var resp = await http.GetAsync("https://api.github.com/rate_limit", ct);
            return new ConnectorHealth { Healthy = resp.IsSuccessStatusCode, Message = resp.IsSuccessStatusCode ? "OK" : $"HTTP {(int)resp.StatusCode}" };
        }
        catch (Exception ex) { return new ConnectorHealth { Healthy = false, Message = ex.Message }; }
    }
}
