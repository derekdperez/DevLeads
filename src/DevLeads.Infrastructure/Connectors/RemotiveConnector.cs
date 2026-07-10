using System.Text.Json;
using Microsoft.Extensions.Logging;
using DevLeads.Core.Connectors;
using DevLeads.Core.Entities;

namespace DevLeads.Infrastructure.Connectors;

/// <summary>
/// Real companies posting paid remote software work via the Remotive job API.
/// Defaults to contract/freelance software roles — businesses actively hiring and
/// willing to spend money — which the pre-filter and AI triage then rank for urgency and fit.
/// </summary>
public sealed class RemotiveConnector : ISourceConnector
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly ILogger<RemotiveConnector> _log;

    public RemotiveConnector(IHttpClientFactory httpFactory, ILogger<RemotiveConnector> log)
    {
        _httpFactory = httpFactory;
        _log = log;
    }

    public string SourceKey => "remotive";
    public string DisplayName => "Remotive (companies hiring)";

    // Only engineering roles are our market — writers/marketers sometimes appear in the
    // software-dev category and must never become leads.
    private static readonly string[] DevRoleSignals =
    {
        "developer", "engineer", "engineering", "devops", "sre", "sysadmin", "architect",
        "full stack", "full-stack", "fullstack", "backend", "back-end", "front end",
        "frontend", "front-end", "software", "programmer", ".net", "dotnet", "c#", "php",
        "python", "node", "react", "wordpress", "shopify", "database", "dba", "cloud",
        "platform", "infrastructure", "api", "integration", "technical lead", "cto"
    };

    private static bool LooksLikeDevRole(string title, string tags) =>
        DevRoleSignals.Any(s => title.Contains(s, StringComparison.OrdinalIgnoreCase) ||
                                tags.Contains(s, StringComparison.OrdinalIgnoreCase));

    public async Task<IReadOnlyList<RawSourceItem>> FetchAsync(SourceConnectorConfig config, CancellationToken ct)
    {
        var category = config.Parameters.TryGetValue("category", out var c) && c.Length > 0 ? c : "software-dev";
        var jobTypes = (config.Parameters.TryGetValue("jobTypes", out var jt) ? jt : "contract;freelance")
            .Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => x.ToLowerInvariant()).ToHashSet();
        var acceptAllTypes = jobTypes.Contains("any");

        var http = _httpFactory.CreateClient(ConnectorSupport.HttpClientName);
        var url = $"https://remotive.com/api/remote-jobs?category={Uri.EscapeDataString(category)}&limit={Math.Min(config.MaxItems * 4, 100)}";
        var items = new List<RawSourceItem>();

        try
        {
            using var doc = JsonDocument.Parse(await http.GetStringAsync(url, ct));
            if (!doc.RootElement.TryGetProperty("jobs", out var jobs)) return items;

            foreach (var j in jobs.EnumerateArray())
            {
                ct.ThrowIfCancellationRequested();
                var jobType = (j.TryGetProperty("job_type", out var jtp) ? jtp.GetString() ?? "" : "").ToLowerInvariant();
                if (!acceptAllTypes && !jobTypes.Contains(jobType)) continue;

                var id = j.TryGetProperty("id", out var idp) ? idp.GetRawText() : "";
                var title = j.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "";
                if (string.IsNullOrWhiteSpace(title)) continue;

                var company = j.TryGetProperty("company_name", out var cn) ? cn.GetString() ?? "" : "";
                var jobUrl = j.TryGetProperty("url", out var u) ? u.GetString() ?? "" : "";
                var salary = j.TryGetProperty("salary", out var sal) ? sal.GetString() ?? "" : "";
                var tags = j.TryGetProperty("tags", out var tg) && tg.ValueKind == JsonValueKind.Array
                    ? string.Join(", ", tg.EnumerateArray().Select(x => x.GetString())) : "";
                if (!LooksLikeDevRole(title, tags)) continue;
                var descHtml = j.TryGetProperty("description", out var dsc) ? dsc.GetString() ?? "" : "";
                var posted = j.TryGetProperty("publication_date", out var pd) && pd.ValueKind == JsonValueKind.String
                    && DateTimeOffset.TryParse(pd.GetString(), out var p) ? p : DateTimeOffset.UtcNow;

                var body =
                    $"Company: {company}\nEngagement: {jobType}\n" +
                    (string.IsNullOrWhiteSpace(salary) ? "" : $"Budget/salary: {salary}\n") +
                    (string.IsNullOrWhiteSpace(tags) ? "" : $"Skills: {tags}\n\n") +
                    StripHtml(descHtml);

                items.Add(ConnectorSupport.NewItem(config.SourceKey, id, title, body, jobUrl,
                    string.IsNullOrWhiteSpace(company) ? null : company, jobUrl, posted, j.GetRawText()));

                if (items.Count >= config.MaxItems) break;
            }
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { _log.LogWarning(ex, "Remotive fetch failed"); }

        return items;
    }

    private static string StripHtml(string html)
    {
        var text = System.Text.RegularExpressions.Regex.Replace(html, "<.*?>", " ");
        text = System.Net.WebUtility.HtmlDecode(text).Trim();
        return text.Length > 4000 ? text[..4000] : text;
    }

    public async Task<ConnectorHealth> CheckHealthAsync(CancellationToken ct)
    {
        try
        {
            var http = _httpFactory.CreateClient(ConnectorSupport.HttpClientName);
            var resp = await http.GetAsync("https://remotive.com/api/remote-jobs?limit=1", ct);
            return new ConnectorHealth { Healthy = resp.IsSuccessStatusCode, Message = resp.IsSuccessStatusCode ? "OK" : $"HTTP {(int)resp.StatusCode}" };
        }
        catch (Exception ex) { return new ConnectorHealth { Healthy = false, Message = ex.Message }; }
    }
}
