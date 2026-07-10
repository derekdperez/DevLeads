using System.Text.Json;
using Microsoft.Extensions.Logging;
using DevLeads.Core.Connectors;
using DevLeads.Core.Entities;

namespace DevLeads.Infrastructure.Connectors;

/// <summary>
/// Open bounties from Opire (https://opire.dev): money attached to public GitHub issues,
/// paid out on merge. Public JSON API, no auth. Items are filtered against the operator's
/// skill profile so only plausibly-fitting bounties are ingested.
/// </summary>
public sealed class OpireConnector : ISourceConnector
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly ILogger<OpireConnector> _log;

    public OpireConnector(IHttpClientFactory httpFactory, ILogger<OpireConnector> log)
    {
        _httpFactory = httpFactory;
        _log = log;
    }

    public string SourceKey => "opire";
    public string DisplayName => "Opire bounties";

    public async Task<IReadOnlyList<RawSourceItem>> FetchAsync(SourceConnectorConfig config, CancellationToken ct)
    {
        var http = _httpFactory.CreateClient(ConnectorSupport.HttpClientName);
        var items = new Dictionary<string, RawSourceItem>();
        var maxPages = GetInt(config, "maxPages", 4); // 30 rewards per page
        var minUsd = GetInt(config, "minAmountUsd", 20);
        // Bounties stay open for months; recency matters less than fit + amount.
        var requireSkillMatch = !config.Parameters.TryGetValue("requireSkillMatch", out var rsm)
                                || !rsm.Equals("false", StringComparison.OrdinalIgnoreCase);

        for (var page = 1; page <= maxPages && items.Count < config.MaxItems; page++)
        {
            ct.ThrowIfCancellationRequested();
            var url = $"https://api.opire.dev/rewards?page={page}";
            try
            {
                using var doc = JsonDocument.Parse(await http.GetStringAsync(url, ct));
                if (doc.RootElement.ValueKind != JsonValueKind.Array) break;
                var any = false;
                foreach (var reward in doc.RootElement.EnumerateArray())
                {
                    any = true;
                    var item = ParseReward(reward, config, minUsd, requireSkillMatch);
                    if (item is not null && !items.ContainsKey(item.ExternalId))
                    {
                        items[item.ExternalId] = item;
                        if (items.Count >= config.MaxItems) break;
                    }
                }
                if (!any) break; // past the last page
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Opire fetch failed for page {Page}", page);
                break;
            }
        }

        return items.Values.Take(config.MaxItems).ToList();
    }

    private RawSourceItem? ParseReward(JsonElement reward, SourceConnectorConfig config, int minUsd, bool requireSkillMatch)
    {
        var id = reward.TryGetProperty("id", out var idEl) ? idEl.GetString() ?? "" : "";
        var title = reward.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "";
        var url = reward.TryGetProperty("url", out var u) ? u.GetString() ?? "" : "";
        if (id.Length == 0 || title.Length == 0 || url.Length == 0) return null;

        double usd = 0;
        if (reward.TryGetProperty("pendingPrice", out var price) &&
            price.ValueKind == JsonValueKind.Object &&
            price.TryGetProperty("value", out var v))
        {
            usd = v.GetDouble() / 100.0; // USD_CENT
        }
        if (usd < minUsd) return null;

        var languages = reward.TryGetProperty("programmingLanguages", out var langs) && langs.ValueKind == JsonValueKind.Array
            ? langs.EnumerateArray().Select(l => l.GetString() ?? "").Where(s => s.Length > 0).ToList()
            : new List<string>();

        string? org = null, orgUrl = null;
        if (reward.TryGetProperty("organization", out var orgEl) && orgEl.ValueKind == JsonValueKind.Object)
        {
            org = orgEl.TryGetProperty("name", out var on) ? on.GetString() : null;
            orgUrl = orgEl.TryGetProperty("url", out var ou) ? ou.GetString() : null;
        }

        // Fit gate: the title/languages must touch the operator's skill profile (when one
        // exists). The URL is deliberately excluded — it would match tool-name skills.
        var matchText = $"{title} {string.Join(' ', languages)}";
        if (requireSkillMatch && config.SkillTerms.Count > 0 &&
            !config.SkillTerms.Any(s => matchText.Contains(s, StringComparison.OrdinalIgnoreCase)))
        {
            return null;
        }

        var posted = reward.TryGetProperty("createdAt", out var created) && created.ValueKind == JsonValueKind.Number
            ? DateTimeOffset.FromUnixTimeMilliseconds((long)created.GetDouble())
            : DateTimeOffset.UtcNow;

        var body = $"Open bounty: ${usd:0} paid on merged fix. " +
                   (languages.Count > 0 ? $"Languages: {string.Join(", ", languages)}. " : "") +
                   $"GitHub issue: {url}";

        return ConnectorSupport.NewItem(config.SourceKey, id, title, body,
            url, org, orgUrl, posted, reward.GetRawText());
    }

    private static int GetInt(SourceConnectorConfig config, string key, int fallback) =>
        config.Parameters.TryGetValue(key, out var raw) && int.TryParse(raw, out var value) ? value : fallback;

    public async Task<ConnectorHealth> CheckHealthAsync(CancellationToken ct)
    {
        try
        {
            var http = _httpFactory.CreateClient(ConnectorSupport.HttpClientName);
            var resp = await http.GetAsync("https://api.opire.dev/rewards?page=1", ct);
            return new ConnectorHealth { Healthy = resp.IsSuccessStatusCode, Message = resp.IsSuccessStatusCode ? "OK" : $"HTTP {(int)resp.StatusCode}" };
        }
        catch (Exception ex) { return new ConnectorHealth { Healthy = false, Message = ex.Message }; }
    }
}
