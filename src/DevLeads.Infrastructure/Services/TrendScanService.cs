using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using DevLeads.Core.Connectors;
using DevLeads.Core.Entities;
using DevLeads.Core.Skills;
using DevLeads.Infrastructure.Data;

namespace DevLeads.Infrastructure.Services;

/// <summary>
/// Polls trend sources (release feeds, vendor blogs, HN, subreddits) and stores
/// skill-relevant items as TrendSignals ranked by hotness. Reuses the lead connectors
/// for fetching but never touches the lead pipeline.
/// </summary>
public sealed class TrendScanService
{
    private readonly DevLeadsDbContext _db;
    private readonly IEnumerable<ISourceConnector> _connectors;
    private readonly DiscoveryActivityTracker _activity;
    private readonly ILogger<TrendScanService> _log;

    public TrendScanService(DevLeadsDbContext db, IEnumerable<ISourceConnector> connectors,
        DiscoveryActivityTracker activity, ILogger<TrendScanService> log)
    {
        _db = db;
        _connectors = connectors;
        _activity = activity;
        _log = log;
    }

    /// <summary>Runs every enabled trend source that is due. Returns new signal count.</summary>
    public async Task<int> RunDueAsync(bool force, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var sources = await _db.TrendSources.Where(s => s.Enabled).ToListAsync(ct);
        var due = force ? sources : sources.Where(s => s.NextRunAt == null || s.NextRunAt <= now).ToList();

        var created = 0;
        foreach (var source in due)
        {
            ct.ThrowIfCancellationRequested();
            created += await RunSourceAsync(source, ct);
        }

        if (due.Count > 0) await PruneOldSignalsAsync(ct);
        return created;
    }

    public async Task<int> RunSourceAsync(TrendSource source, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var previousRunAt = source.LastRunAt;
        source.LastRunAt = now;
        source.NextRunAt = now.AddMinutes(Math.Max(30, source.PollIntervalMinutes));

        var connector = _connectors.FirstOrDefault(c => c.SourceKey == source.Kind);
        if (connector is null)
        {
            source.LastRunHealthy = false;
            source.LastRunMessage = $"No connector registered for kind '{source.Kind}'.";
            await _db.SaveChangesAsync(ct);
            return 0;
        }

        var skills = await _db.Skills.AsNoTracking().Where(s => s.Enabled).ToListAsync(ct);
        var config = new SourceConnectorConfig
        {
            SourceKey = source.SeedKey,
            MaxItems = source.MaxItemsPerRun,
            Terms = SkillMatcher.SearchTerms(skills),
            Since = previousRunAt?.AddHours(-2),
            Parameters = ParseParameters(source.ParametersJson),
            SkillTerms = SkillMatcher.SearchTerms(skills)
        };

        var created = 0;
        _activity.RunStarted(source.SeedKey, source.DisplayName);
        try
        {
            var items = await connector.FetchAsync(config, ct);
            var seen = await GetSeenExternalIdsAsync(source.SeedKey, items.Select(i => i.ExternalId), ct);
            var skippedOffSkill = 0;

            foreach (var item in items)
            {
                if (string.IsNullOrWhiteSpace(item.Url) || seen.Contains(item.ExternalId)) continue;

                var text = $"{item.Title}\n{item.BodyText}";
                var matches = SkillMatcher.Match(text, skills);
                if (source.RequireSkillMatch && matches.Count == 0) { skippedOffSkill++; continue; }

                var engagement = ExtractEngagement(item.RawJson);
                _db.TrendSignals.Add(new TrendSignal
                {
                    SourceKey = source.SeedKey,
                    ExternalId = item.ExternalId,
                    Url = item.Url,
                    Title = item.Title,
                    Snippet = Compact(item.BodyText, 400),
                    PostedAt = item.PostedAt,
                    FetchedAt = now,
                    Engagement = engagement,
                    MatchedSkillsJson = JsonSerializer.Serialize(matches.Select(m => m.Name)),
                    Hotness = ComputeHotness(matches, engagement, item.PostedAt, now)
                });
                created++;
                seen.Add(item.ExternalId);
            }

            source.LastRunHealthy = true;
            source.LastRunItemCount = items.Count;
            source.LastRunMessage = $"Fetched {items.Count} item(s); {created} new signal(s)" +
                (skippedOffSkill > 0 ? $"; {skippedOffSkill} off-skill." : ".");
        }
        catch (OperationCanceledException)
        {
            _activity.RunCompleted(source.SeedKey, healthy: false, "Trend scan cancelled.");
            throw;
        }
        catch (Exception ex)
        {
            source.LastRunHealthy = false;
            source.LastRunMessage = ex.Message;
            _log.LogError(ex, "Trend scan failed for {Source}", source.SeedKey);
        }

        _activity.RunCompleted(source.SeedKey, source.LastRunHealthy, $"{source.DisplayName}: {source.LastRunMessage}");
        await _db.SaveChangesAsync(ct);
        return created;
    }

    /// <summary>Skill relevance dominates; platform engagement and freshness break ties.</summary>
    private static double ComputeHotness(
        IReadOnlyList<SkillMatch> matches, double engagement, DateTimeOffset postedAt, DateTimeOffset now)
    {
        var skillScore = matches.Sum(m => m.Weight) * 8.0;
        var engagementScore = Math.Min(engagement, 400) * 0.15;
        var ageHours = (now - postedAt).TotalHours;
        var recency = ageHours <= 48 ? 25.0 : ageHours <= 168 ? 12.0 : 0.0;
        return Math.Round(skillScore + engagementScore + recency, 1);
    }

    /// <summary>HN Algolia hits carry points/num_comments in the raw JSON; other feeds don't.</summary>
    private static double ExtractEngagement(string rawJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(rawJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return 0;
            double value = 0;
            if (doc.RootElement.TryGetProperty("points", out var p) && p.ValueKind == JsonValueKind.Number)
                value += p.GetDouble();
            if (doc.RootElement.TryGetProperty("num_comments", out var c) && c.ValueKind == JsonValueKind.Number)
                value += c.GetDouble();
            return value;
        }
        catch { return 0; }
    }

    private async Task<HashSet<string>> GetSeenExternalIdsAsync(
        string sourceKey, IEnumerable<string> ids, CancellationToken ct)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var chunk in ids.Distinct().Chunk(500))
        {
            var found = await _db.TrendSignals.AsNoTracking()
                .Where(s => s.SourceKey == sourceKey && chunk.Contains(s.ExternalId))
                .Select(s => s.ExternalId)
                .ToListAsync(ct);
            seen.UnionWith(found);
        }
        return seen;
    }

    /// <summary>Trend evidence goes stale fast; anything past 30 days is dead weight.</summary>
    private async Task PruneOldSignalsAsync(CancellationToken ct)
    {
        var cutoff = DateTimeOffset.UtcNow.AddDays(-30);
        var old = await _db.TrendSignals.Where(s => s.PostedAt < cutoff).ToListAsync(ct);
        if (old.Count == 0) return;
        _db.TrendSignals.RemoveRange(old);
        await _db.SaveChangesAsync(ct);
    }

    private static IReadOnlyDictionary<string, string> ParseParameters(string json)
    {
        var dict = new Dictionary<string, string>();
        try
        {
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return dict;
            foreach (var prop in doc.RootElement.EnumerateObject())
                dict[prop.Name] = prop.Value.ValueKind == JsonValueKind.String
                    ? prop.Value.GetString() ?? ""
                    : prop.Value.GetRawText();
        }
        catch { /* malformed config -> empty */ }
        return dict;
    }

    private static string Compact(string value, int max)
    {
        var compact = string.Join(' ', (value ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return compact.Length <= max ? compact : compact[..max];
    }
}
