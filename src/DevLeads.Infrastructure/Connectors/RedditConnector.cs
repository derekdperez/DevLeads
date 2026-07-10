using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using DevLeads.Core.Connectors;
using DevLeads.Core.Entities;

namespace DevLeads.Infrastructure.Connectors;

/// <summary>
/// Read-only ingestion of new posts from configured subreddits (manual response preferred).
/// Uses the Atom feeds (new.rss / search.rss) — Reddit's unauthenticated JSON API returns
/// 403 from datacenter IPs, while the RSS endpoints stay open.
/// </summary>
public sealed class RedditConnector : ISourceConnector
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly ILogger<RedditConnector> _log;

    public RedditConnector(IHttpClientFactory httpFactory, ILogger<RedditConnector> log)
    {
        _httpFactory = httpFactory;
        _log = log;
    }

    public string SourceKey => "reddit";
    public string DisplayName => "Reddit (read-only)";

    public async Task<IReadOnlyList<RawSourceItem>> FetchAsync(SourceConnectorConfig config, CancellationToken ct)
    {
        var subs = (config.Parameters.TryGetValue("subreddits", out var s) ? s : "forhire;jobbit;hireawebdeveloper")
            .Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var requireHiring = config.Parameters.TryGetValue("requireHiring", out var rh) &&
                            rh.Equals("true", StringComparison.OrdinalIgnoreCase);
        var http = _httpFactory.CreateClient(ConnectorSupport.HttpClientName);
        var items = new Dictionary<string, RawSourceItem>();
        var daysBack = GetInt(config, "daysBack", 7);
        var cutoff = DateTimeOffset.UtcNow.AddDays(-daysBack);

        // One multireddit request covers every configured sub ("r/a+b+c/new.rss") —
        // the per-IP RSS budget is tiny, so requests are the scarce resource here.
        var multi = string.Join('+', subs.Select(Uri.EscapeDataString));

        if (requireHiring)
        {
            // Hiring subs: read the newest posts and keep only [Hiring]/[Task] offers —
            // people explicitly ready to pay. Precision comes from pre-filter + AI triage.
            var url = $"https://www.reddit.com/r/{multi}/new.rss?limit={Math.Clamp(config.MaxItems, 25, 100)}";
            await FetchListingAsync(http, url, multi, config.SourceKey, items, config.MaxItems, cutoff, ct,
                title => title.Contains("hiring", StringComparison.OrdinalIgnoreCase) ||
                         title.Contains("[task]", StringComparison.OrdinalIgnoreCase) ||
                         title.Contains("[paid]", StringComparison.OrdinalIgnoreCase));
        }
        else
        {
            // Topical subs: read regular subreddit traffic and let the local pre-filter
            // find pain signals. This avoids hammering Reddit search once per term.
            var searchMode = config.Parameters.TryGetValue("searchMode", out var sm) ? sm : "new";
            if (!searchMode.Equals("search", StringComparison.OrdinalIgnoreCase))
            {
                var url = $"https://www.reddit.com/r/{multi}/new.rss?limit={Math.Clamp(config.MaxItems, 25, 100)}";
                await FetchListingAsync(http, url, multi, config.SourceKey, items, config.MaxItems, cutoff, ct, _ => true);
                return items.Values.Take(config.MaxItems).ToList();
            }

            // Optional search mode for tightly scoped investigations.
            var terms = PickSearchTerms(config.Terms, 8);
            var perQuery = Math.Clamp(config.MaxItems / Math.Max(1, subs.Length * Math.Max(1, terms.Count)), 3, 10);
            foreach (var sub in subs)
            {
                foreach (var term in terms)
                {
                    ct.ThrowIfCancellationRequested();
                    var url = $"https://www.reddit.com/r/{Uri.EscapeDataString(sub)}/search.rss?q={Uri.EscapeDataString(term)}&restrict_sr=1&sort=new&t=week&limit={perQuery}";
                    if (!await FetchListingAsync(http, url, sub, config.SourceKey, items, config.MaxItems, cutoff, ct, _ => true))
                        return items.Values.Take(config.MaxItems).ToList(); // rate-limited
                    if (items.Count >= config.MaxItems) break;
                }
                if (items.Count >= config.MaxItems) break;
            }
        }

        return items.Values.Take(config.MaxItems).ToList();
    }

    private static readonly XNamespace Atom = "http://www.w3.org/2005/Atom";

    /// <summary>Fetches one subreddit feed. Returns false when rate-limited (callers stop the run).</summary>
    private async Task<bool> FetchListingAsync(HttpClient http, string url, string sub, string sourceKey,
        Dictionary<string, RawSourceItem> items, int maxItems, DateTimeOffset cutoff,
        CancellationToken ct, Func<string, bool> titleFilter)
    {
        try
        {
            using var resp = await http.GetAsync(url, ct);
            if ((int)resp.StatusCode is 429 or 403)
            {
                _log.LogWarning("Reddit rate-limited (HTTP {Status}) at r/{Sub} — aborting this run.", (int)resp.StatusCode, sub);
                return false;
            }
            resp.EnsureSuccessStatusCode();

            var root = XDocument.Parse(await resp.Content.ReadAsStringAsync(ct)).Root;
            if (root is null) return true;

            foreach (var entry in root.Elements(Atom + "entry"))
            {
                // Atom ids look like "t3_1abcd2" — keep the bare post id.
                var id = ((string?)entry.Element(Atom + "id") ?? "").Replace("t3_", "");
                if (id.Length == 0 || items.ContainsKey(id)) continue;

                var title = System.Net.WebUtility.HtmlDecode((string?)entry.Element(Atom + "title") ?? "");
                if (string.IsNullOrWhiteSpace(title) || !titleFilter(title)) continue;

                var body = StripHtml((string?)entry.Element(Atom + "content") ?? "");
                var link = (string?)entry.Element(Atom + "link")?.Attribute("href") ?? "";
                if (link.Length == 0) continue;

                var authorEl = entry.Element(Atom + "author");
                var author = ((string?)authorEl?.Element(Atom + "name"))?.TrimStart('/', 'u').TrimStart('/');
                var authorUrl = (string?)authorEl?.Element(Atom + "uri");

                var postedRaw = (string?)entry.Element(Atom + "published") ?? (string?)entry.Element(Atom + "updated");
                var posted = DateTimeOffset.TryParse(postedRaw, out var p) ? p : DateTimeOffset.UtcNow;
                if (posted < cutoff) continue;

                items[id] = ConnectorSupport.NewItem(sourceKey, id, title, body, link, author,
                    authorUrl ?? (author is null ? null : $"https://www.reddit.com/user/{author}"),
                    posted, entry.ToString());
                if (items.Count >= maxItems) break;
            }
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { _log.LogWarning(ex, "Reddit fetch failed for r/{Sub}", sub); }
        return true;
    }

    private static string StripHtml(string html) =>
        System.Net.WebUtility.HtmlDecode(System.Text.RegularExpressions.Regex.Replace(html, "<.*?>", " ")).Trim();

    private static List<string> PickSearchTerms(IEnumerable<string> terms, int max) =>
        terms.Where(t => !string.IsNullOrWhiteSpace(t))
            .OrderByDescending(t => t.Length)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(max)
            .ToList();

    private static int GetInt(SourceConnectorConfig config, string key, int fallback) =>
        config.Parameters.TryGetValue(key, out var raw) && int.TryParse(raw, out var value) ? value : fallback;

    public async Task<ConnectorHealth> CheckHealthAsync(CancellationToken ct)
    {
        try
        {
            var http = _httpFactory.CreateClient(ConnectorSupport.HttpClientName);
            var resp = await http.GetAsync("https://www.reddit.com/r/webdev/new.rss?limit=1", ct);
            return new ConnectorHealth { Healthy = resp.IsSuccessStatusCode, Message = resp.IsSuccessStatusCode ? "OK" : $"HTTP {(int)resp.StatusCode}" };
        }
        catch (Exception ex) { return new ConnectorHealth { Healthy = false, Message = ex.Message }; }
    }
}
