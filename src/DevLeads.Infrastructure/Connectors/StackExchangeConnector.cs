using System.Text.Json;
using Microsoft.Extensions.Logging;
using DevLeads.Core.Connectors;
using DevLeads.Core.Entities;

namespace DevLeads.Infrastructure.Connectors;

/// <summary>Fresh technical problem detection via the Stack Exchange API.</summary>
public sealed class StackExchangeConnector : ISourceConnector
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly ILogger<StackExchangeConnector> _log;

    public StackExchangeConnector(IHttpClientFactory httpFactory, ILogger<StackExchangeConnector> log)
    {
        _httpFactory = httpFactory;
        _log = log;
    }

    public string SourceKey => "stackexchange";
    public string DisplayName => "Stack Exchange";

    public async Task<IReadOnlyList<RawSourceItem>> FetchAsync(SourceConnectorConfig config, CancellationToken ct)
    {
        // Supports multiple sites (e.g. "serverfault;stackoverflow"); Server Fault skews
        // toward businesses with real production/ops incidents.
        var sitesRaw = config.Parameters.TryGetValue("sites", out var multi) && multi.Length > 0 ? multi
            : config.Parameters.TryGetValue("site", out var single) ? single : "serverfault;stackoverflow";
        var sites = sitesRaw.Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var http = _httpFactory.CreateClient(ConnectorSupport.HttpClientName);
        var items = new Dictionary<string, RawSourceItem>();
        var daysBack = GetInt(config, "daysBack", 7);
        var fromDate = (config.Since ?? DateTimeOffset.UtcNow.AddDays(-daysBack)).ToUnixTimeSeconds();
        var terms = PickSearchTerms(config.Terms, Math.Max(2, 8 / sites.Length));
        var perQuery = Math.Clamp(config.MaxItems / Math.Max(1, sites.Length * terms.Count), 4, 12);

        foreach (var (site, term) in sites.SelectMany(site => terms.Select(term => (site, term))))
        {
            ct.ThrowIfCancellationRequested();
            // No `tagged` constraint: search/advanced ANDs the tag list, so a multi-tag
            // filter silently returns zero results. The q term + pre-filter judge relevance.
            var url = $"https://api.stackexchange.com/2.3/search/advanced?order=desc&sort=creation&site={Uri.EscapeDataString(site)}" +
                      $"&q={Uri.EscapeDataString(term)}&fromdate={fromDate}&pagesize={perQuery}&filter=withbody";
            try
            {
                using var resp = await http.GetAsync(url, ct);
                var json = await resp.Content.ReadAsStringAsync(ct);
                if (!resp.IsSuccessStatusCode || json.Contains("throttle_violation", StringComparison.OrdinalIgnoreCase))
                {
                    // The anonymous quota is per-IP for the whole day; keeping going only
                    // extends the ban. Abort the run and let the next poll retry.
                    _log.LogWarning("Stack Exchange throttled/failed (HTTP {Status}) — aborting run.", (int)resp.StatusCode);
                    break;
                }
                using var doc = JsonDocument.Parse(json);
                if (!doc.RootElement.TryGetProperty("items", out var arr)) continue;
                foreach (var q in arr.EnumerateArray())
                {
                    var id = $"{site}-{q.GetProperty("question_id").GetInt64()}";
                    if (items.ContainsKey(id)) continue;
                    var title = System.Net.WebUtility.HtmlDecode(q.TryGetProperty("title", out var ti) ? ti.GetString() ?? "" : "");
                    var body = StripHtml(q.TryGetProperty("body", out var b) ? b.GetString() ?? "" : "");
                    var link = q.TryGetProperty("link", out var l) ? l.GetString() ?? "" : "";
                    var posted = q.TryGetProperty("creation_date", out var cd)
                        ? DateTimeOffset.FromUnixTimeSeconds(cd.GetInt64()) : DateTimeOffset.UtcNow;
                    string? author = null, authorUrl = null;
                    if (q.TryGetProperty("owner", out var owner) && owner.ValueKind == JsonValueKind.Object)
                    {
                        author = owner.TryGetProperty("display_name", out var dn) ? dn.GetString() : null;
                        authorUrl = owner.TryGetProperty("link", out var ol) ? ol.GetString() : null;
                    }
                    items[id] = ConnectorSupport.NewItem(config.SourceKey, id, title, body, link, author, authorUrl, posted, q.GetRawText());
                    if (items.Count >= config.MaxItems) break;
                }
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) { _log.LogWarning(ex, "Stack Exchange fetch failed for term {Term}", term); }

            if (items.Count >= config.MaxItems) break;
        }

        return items.Values.Take(config.MaxItems).ToList();
    }

    private static string StripHtml(string html)
    {
        var text = System.Text.RegularExpressions.Regex.Replace(html, "<.*?>", " ");
        return System.Net.WebUtility.HtmlDecode(text).Trim();
    }

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
            var resp = await http.GetAsync("https://api.stackexchange.com/2.3/info?site=stackoverflow", ct);
            return new ConnectorHealth { Healthy = resp.IsSuccessStatusCode, Message = resp.IsSuccessStatusCode ? "OK" : $"HTTP {(int)resp.StatusCode}" };
        }
        catch (Exception ex) { return new ConnectorHealth { Healthy = false, Message = ex.Message }; }
    }
}
