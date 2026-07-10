using System.Text.Json;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using DevLeads.Core.Connectors;
using DevLeads.Core.Entities;

namespace DevLeads.Infrastructure.Connectors;

/// <summary>Simple, reliable ingestion of RSS / Atom feeds configured per source.</summary>
public sealed class RssConnector : ISourceConnector
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly ILogger<RssConnector> _log;

    public RssConnector(IHttpClientFactory httpFactory, ILogger<RssConnector> log)
    {
        _httpFactory = httpFactory;
        _log = log;
    }

    public string SourceKey => "rss";
    public string DisplayName => "RSS / forum feeds";

    private static string[] Feeds(SourceConnectorConfig config)
    {
        if (config.Parameters.TryGetValue("feeds", out var raw) && !string.IsNullOrWhiteSpace(raw))
        {
            try
            {
                using var doc = JsonDocument.Parse(raw);
                if (doc.RootElement.ValueKind == JsonValueKind.Array)
                    return doc.RootElement.EnumerateArray().Select(e => e.GetString() ?? "").Where(u => u.Length > 0).ToArray();
            }
            catch { return raw.Split(new[] { ',', ';', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries); }
        }
        return Array.Empty<string>();
    }

    public async Task<IReadOnlyList<RawSourceItem>> FetchAsync(SourceConnectorConfig config, CancellationToken ct)
    {
        var http = _httpFactory.CreateClient(ConnectorSupport.HttpClientName);
        var items = new Dictionary<string, RawSourceItem>();

        // Job/forum feeds keep entries visible for weeks; age is the right freshness
        // filter here (dedup already prevents re-ingesting previously seen posts).
        var daysBack = config.Parameters.TryGetValue("daysBack", out var db) && int.TryParse(db, out var d) ? d : 21;
        var cutoff = DateTimeOffset.UtcNow.AddDays(-daysBack);

        foreach (var feed in Feeds(config))
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var xml = await http.GetStringAsync(feed, ct);
                var root = XDocument.Parse(xml).Root;
                if (root is null) continue;
                XNamespace atom = "http://www.w3.org/2005/Atom";

                // RSS 2.0
                foreach (var el in root.Descendants("item"))
                {
                    var link = (string?)el.Element("link") ?? "";
                    var title = (string?)el.Element("title") ?? "";
                    var desc = (string?)el.Element("description") ?? "";
                    var pub = (string?)el.Element("pubDate");
                    var posted = DateTimeOffset.TryParse(pub, out var p) ? p : DateTimeOffset.UtcNow;
                    var id = (string?)el.Element("guid") ?? link;
                    if (string.IsNullOrWhiteSpace(title)) continue;
                    if (posted < cutoff) continue;
                    var item = ConnectorSupport.NewItem(config.SourceKey, id, title, StripHtml(desc), link, null, null, posted, el.ToString());
                    await EnrichForumThreadAsync(http, item, ct);
                    items.TryAdd(id, item);
                    if (items.Count >= config.MaxItems) break;
                }
                // Atom
                foreach (var el in root.Elements(atom + "entry"))
                {
                    if (items.Count >= config.MaxItems) break;
                    var title = (string?)el.Element(atom + "title") ?? "";
                    var link = el.Elements(atom + "link").FirstOrDefault()?.Attribute("href")?.Value ?? "";
                    var summary = (string?)el.Element(atom + "summary") ?? (string?)el.Element(atom + "content") ?? "";
                    var id = (string?)el.Element(atom + "id") ?? link;
                    var updated = (string?)el.Element(atom + "updated");
                    var posted = DateTimeOffset.TryParse(updated, out var p) ? p : DateTimeOffset.UtcNow;
                    if (string.IsNullOrWhiteSpace(title)) continue;
                    if (posted < cutoff) continue;
                    var item = ConnectorSupport.NewItem(config.SourceKey, id, title, StripHtml(summary), link, null, null, posted, el.ToString());
                    await EnrichForumThreadAsync(http, item, ct);
                    items.TryAdd(id, item);
                }
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) { _log.LogWarning(ex, "RSS fetch failed for {Feed}", feed); }

            if (items.Count >= config.MaxItems) break;
        }

        return items.Values.Take(config.MaxItems).ToList();
    }

    private static string StripHtml(string html) =>
        System.Net.WebUtility.HtmlDecode(System.Text.RegularExpressions.Regex.Replace(html, "<.*?>", " ")).Trim();

    private static async Task EnrichForumThreadAsync(HttpClient http, RawSourceItem item, CancellationToken ct)
    {
        if (!Uri.TryCreate(item.Url, UriKind.Absolute, out var uri)) return;
        if (!uri.Host.Contains('.', StringComparison.OrdinalIgnoreCase)) return;
        if (!item.Url.Contains("/t/", StringComparison.OrdinalIgnoreCase)) return;

        var jsonUrl = item.Url.TrimEnd('/') + ".json";
        try
        {
            var json = await http.GetStringAsync(jsonUrl, ct);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var accepted = HasTruthyProperty(root, "accepted_answer") ||
                           HasTruthyProperty(root, "has_accepted_answer") ||
                           HasTruthyProperty(root, "accepted_answer_post_id") ||
                           json.Contains("accepted_answer", StringComparison.OrdinalIgnoreCase);
            var closed = HasTruthyProperty(root, "closed") || HasTruthyProperty(root, "archived");

            var updated = GetDate(root, "last_posted_at") ??
                          GetDate(root, "bumped_at") ??
                          GetDate(root, "updated_at");
            if (updated is { } sourceUpdated)
                item.PostedAt = sourceUpdated;

            if (accepted || closed)
            {
                item.BodyText += "\n\nThread status: " +
                                 string.Join(", ", new[]
                                 {
                                     accepted ? "accepted_answer" : "",
                                     closed ? "closed_or_archived" : ""
                                 }.Where(s => s.Length > 0));
            }

            item.RawJson = json;
        }
        catch
        {
            // Feed content is still usable if optional forum metadata is unavailable.
        }
    }

    private static bool HasTruthyProperty(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var value)) return false;
        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.Number => value.TryGetInt64(out var n) && n > 0,
            JsonValueKind.String => !string.IsNullOrWhiteSpace(value.GetString()),
            JsonValueKind.Object => true,
            JsonValueKind.Array => value.GetArrayLength() > 0,
            _ => false
        };
    }

    private static DateTimeOffset? GetDate(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) &&
        value.ValueKind == JsonValueKind.String &&
        DateTimeOffset.TryParse(value.GetString(), out var date)
            ? date
            : null;

    public async Task<ConnectorHealth> CheckHealthAsync(CancellationToken ct)
    {
        await Task.CompletedTask;
        return new ConnectorHealth { Healthy = true, Message = "Configured feeds are fetched on run." };
    }
}
