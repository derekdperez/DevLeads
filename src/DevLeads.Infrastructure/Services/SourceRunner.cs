using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using DevLeads.Core;
using DevLeads.Core.Ai;
using DevLeads.Core.Connectors;
using DevLeads.Core.Entities;
using DevLeads.Core.QueryPacks;
using DevLeads.Infrastructure.Ai;
using DevLeads.Infrastructure.Data;

namespace DevLeads.Infrastructure.Services;

/// <summary>Runs one source end-to-end: fetch via its connector, then ingest each item.</summary>
public sealed class SourceRunner
{
    private readonly DevLeadsDbContext _db;
    private readonly IEnumerable<ISourceConnector> _connectors;
    private readonly IQueryPackProvider _queryPacks;
    private readonly LeadIngestionService _ingestion;
    private readonly HeuristicPreFilter _preFilter;
    private readonly AiTriageRouter _ai;
    private readonly DiscoveryActivityTracker _activity;
    private readonly ILogger<SourceRunner> _log;

    public SourceRunner(DevLeadsDbContext db, IEnumerable<ISourceConnector> connectors,
        IQueryPackProvider queryPacks, LeadIngestionService ingestion, HeuristicPreFilter preFilter,
        AiTriageRouter ai, DiscoveryActivityTracker activity, ILogger<SourceRunner> log)
    {
        _db = db;
        _connectors = connectors;
        _queryPacks = queryPacks;
        _ingestion = ingestion;
        _preFilter = preFilter;
        _ai = ai;
        _activity = activity;
        _log = log;
    }

    /// <summary>Fetches and ingests for a single source config. Returns the number of new opportunities.</summary>
    public async Task<int> RunAsync(SourceConfig source, CancellationToken ct)
    {
        var parameters = ParseParameters(source.ParametersJson);
        var connectorKey = ResolveConnectorKey(source.SourceKey, parameters);
        var connector = _connectors.FirstOrDefault(c => c.SourceKey == connectorKey);
        var now = DateTimeOffset.UtcNow;
        var previousRunAt = source.LastRunAt; // capture BEFORE overwriting — this is the fetch window start
        source.LastRunAt = now;
        source.NextRunAt = now.AddMinutes(Math.Max(1, source.PollIntervalMinutes));

        if (connector is null)
        {
            source.LastRunHealthy = false;
            source.LastRunMessage = "No connector registered for this source.";
            await _db.SaveChangesAsync(ct);
            return 0;
        }

        var skills = await _db.Skills.AsNoTracking().Where(s => s.Enabled).ToListAsync(ct);
        var config = new SourceConnectorConfig
        {
            SourceKey = source.SourceKey,
            MaxItems = source.MaxItemsPerRun,
            Terms = BuildTerms(source),
            // Overlap the window slightly so posts published mid-run aren't missed;
            // dedup drops anything already seen.
            Since = previousRunAt?.AddMinutes(-Math.Max(30, source.PollIntervalMinutes * 2)),
            Parameters = parameters,
            SkillTerms = Core.Skills.SkillMatcher.SearchTerms(skills)
        };

        int created = 0, skipped = 0, shortlistRejected = 0;
        ShortlistGate shortlist = ShortlistGate.Disabled;
        _activity.RunStarted(source.SourceKey, source.DisplayName);
        try
        {
            var items = await connector.FetchAsync(config, ct);
            var settings = await _db.OperatorSettings.AsNoTracking().FirstOrDefaultAsync(ct) ?? new OperatorSettings();

            // AI spend goes to unseen items only: re-fetched posts are already recorded
            // (and triaged) — screening them again would burn calls for nothing.
            var seenIds = await GetSeenExternalIdsAsync(source.SourceKey, items, ct);
            var newItems = items.Where(i => !seenIds.Contains(i.ExternalId)).ToList();
            var packNames = PackNames(source);
            var preFilterByItem = newItems.ToDictionary(i => i, i => _preFilter.Analyze(i, packNames));
            var campaignObjective = await GetCampaignObjectiveAsync(source, ct);

            shortlist = await BuildShortlistGateAsync(newItems, preFilterByItem, source, parameters, settings, campaignObjective, ct);
            var batchTriage = await BatchTriageAsync(newItems, preFilterByItem, shortlist, source, parameters, settings, campaignObjective, ct);
            foreach (var item in items)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    if (shortlist.ShouldRecordRawOnly(item))
                    {
                        if (await _ingestion.RecordRawOnlyAsync(item, ct))
                            shortlistRejected++;
                        continue;
                    }

                    // IngestAsync returns null for duplicates and for posts triaged as non-payable.
                    var opp = await _ingestion.IngestAsync(item, source, ct, batchTriage.GetValueOrDefault(item));
                    if (opp is not null)
                    {
                        created++;
                        _activity.LeadCreated(source.SourceKey, opp.Title, opp.Score);
                    }
                }
                catch (InvalidOperationException ex) when (ex.Message.Contains("valid source URL", StringComparison.OrdinalIgnoreCase))
                {
                    skipped++;
                    _log.LogWarning(ex, "Skipping source item without a valid URL from {Source}", source.SourceKey);
                }
            }
            source.LastRunHealthy = true;
            source.LastRunItemCount = items.Count;
            source.LastRunMessage = BuildRunMessage(items.Count, created, skipped, shortlist, shortlistRejected);
        }
        catch (OperationCanceledException)
        {
            _activity.RunCompleted(source.SourceKey, healthy: false, "Run cancelled.");
            throw;
        }
        catch (Exception ex)
        {
            source.LastRunHealthy = false;
            source.LastRunMessage = ex.Message;
            _log.LogError(ex, "Source run failed for {Source}", source.SourceKey);
        }

        _activity.RunCompleted(source.SourceKey, source.LastRunHealthy, $"{source.DisplayName}: {source.LastRunMessage}");
        await _db.SaveChangesAsync(ct);
        return created;
    }

    /// <summary>Which of these items' external ids are already recorded for this source.</summary>
    private async Task<HashSet<string>> GetSeenExternalIdsAsync(
        string sourceKey, IReadOnlyList<RawSourceItem> items, CancellationToken ct)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var ids = items.Select(i => i.ExternalId).Distinct().ToList();
        foreach (var chunk in ids.Chunk(500)) // stay under SQLite's parameter limit
        {
            var found = await _db.RawSourceItems.AsNoTracking()
                .Where(r => r.SourceKey == sourceKey && chunk.Contains(r.ExternalId))
                .Select(r => r.ExternalId)
                .ToListAsync(ct);
            seen.UnionWith(found);
        }
        return seen;
    }

    /// <summary>
    /// Triages the shortlist survivors in chunked batch calls (one model call per
    /// <see cref="AiTriageRouter.BatchTriageChunkSize"/> items) so ingestion doesn't
    /// spend a call per lead. Items missing from the result use the per-item path.
    /// </summary>
    private async Task<Dictionary<RawSourceItem, AiTriageResponse>> BatchTriageAsync(
        IReadOnlyList<RawSourceItem> newItems,
        IReadOnlyDictionary<RawSourceItem, PreFilterResult> preFilterByItem,
        ShortlistGate shortlist,
        SourceConfig source,
        IReadOnlyDictionary<string, string> parameters,
        OperatorSettings settings,
        string campaignObjective,
        CancellationToken ct)
    {
        var results = new Dictionary<RawSourceItem, AiTriageResponse>();
        var triageSettings = ResolveTriageSettings(settings, parameters);
        if (triageSettings.AiProvider.Equals("Heuristic", StringComparison.OrdinalIgnoreCase))
            return results;

        var eligible = newItems.Where(i =>
                preFilterByItem[i].ShouldAnalyzeWithAi &&
                (double)preFilterByItem[i].HeuristicScore >= source.MinPreFilterScore &&
                !shortlist.ShouldRecordRawOnly(i))
            .ToList();
        if (eligible.Count < 2) return results; // a single item costs the same either way

        var skills = await _db.Skills.AsNoTracking().Where(s => s.Enabled).ToListAsync(ct);
        var operatorSkills = skills.Count == 0 ? "" : Core.Skills.SkillMatcher.PromptSummary(skills);

        foreach (var chunk in eligible.Chunk(AiTriageRouter.BatchTriageChunkSize))
        {
            if (await _ingestion.IsOverAiBudgetAsync(settings, ct)) break;

            var batchItems = chunk.Select((item, idx) => new AiBatchTriageItem
            {
                Id = "b" + idx,
                Request = new AiTriageRequest
                {
                    Title = item.Title,
                    Body = Compact(item.BodyText, 1800),
                    SourceKey = item.SourceKey,
                    PostedAt = item.PostedAt,
                    MatchedTerms = preFilterByItem[item].MatchedTerms,
                    HeuristicScore = preFilterByItem[item].HeuristicScore,
                    OperatorSkills = operatorSkills,
                    CampaignObjective = campaignObjective
                }
            }).ToList();

            var resp = await _ai.TriageBatchAsync(batchItems, triageSettings, ct);
            if (!resp.Succeeded)
            {
                _log.LogWarning("Batch triage for {Source} failed ({Error}); items fall back to per-item triage.",
                    source.SourceKey, resp.ErrorMessage);
                continue;
            }

            for (var i = 0; i < chunk.Length; i++)
            {
                if (!resp.Results.TryGetValue("b" + i, out var result)) continue;
                results[chunk[i]] = new AiTriageResponse
                {
                    Succeeded = true,
                    Result = result,
                    Provider = resp.Provider + "/batch",
                    Model = resp.Model,
                    RequestJson = JsonSerializer.Serialize(new { batch = true, size = chunk.Length }),
                    ResponseJson = JsonSerializer.Serialize(result)
                };
            }
        }

        if (results.Count > 0)
            _log.LogInformation("Batch triage for {Source}: {Count} item(s) triaged in {Calls} call(s).",
                source.SourceKey, results.Count,
                (eligible.Count + AiTriageRouter.BatchTriageChunkSize - 1) / AiTriageRouter.BatchTriageChunkSize);
        return results;
    }

    private async Task<ShortlistGate> BuildShortlistGateAsync(
        IReadOnlyList<RawSourceItem> items,
        IReadOnlyDictionary<RawSourceItem, PreFilterResult> preFilterByItem,
        SourceConfig source,
        IReadOnlyDictionary<string, string> parameters,
        OperatorSettings settings,
        string campaignObjective,
        CancellationToken ct)
    {
        if (!ShouldUseBatchShortlist(parameters, ResolveTriageProvider(parameters, settings)))
            return ShortlistGate.Disabled;

        var candidateIds = new Dictionary<RawSourceItem, string>();
        var requestItems = new List<AiShortlistItem>();
        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            var pre = preFilterByItem[item];
            if (!pre.ShouldAnalyzeWithAi || (double)pre.HeuristicScore < source.MinPreFilterScore)
                continue;

            var id = "i" + i.ToString(System.Globalization.CultureInfo.InvariantCulture);
            candidateIds[item] = id;
            requestItems.Add(new AiShortlistItem
            {
                Id = id,
                Title = item.Title,
                Snippet = Compact(item.BodyText, 700),
                SourceKey = item.SourceKey,
                PostedAt = item.PostedAt,
                HeuristicScore = pre.HeuristicScore,
                MatchedTerms = pre.MatchedTerms
            });
        }

        var minCandidates = GetInt(parameters, "shortlistMinCandidates", 2);
        if (requestItems.Count < minCandidates)
            return ShortlistGate.Disabled;

        var maxSelections = ResolveShortlistMax(parameters, requestItems.Count);
        if (maxSelections >= requestItems.Count)
            return ShortlistGate.Disabled;

        var shortlistSettings = ResolveTriageSettings(settings, parameters);
        var response = await _ai.ShortlistAsync(requestItems, shortlistSettings, maxSelections, campaignObjective, ct);
        var selectedIds = response.Decisions
            .Where(d => d.ShouldTriage)
            .Select(d => d.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        _log.LogInformation("Batch shortlist for {Source} selected {Selected}/{Candidates} candidates via {Provider}.",
            source.SourceKey, selectedIds.Count, requestItems.Count, response.Provider);

        return new ShortlistGate(enabled: true, candidateIds, selectedIds, requestItems.Count, response.Provider);
    }

    private static bool ShouldUseBatchShortlist(IReadOnlyDictionary<string, string> parameters, string provider)
    {
        var defaultEnabled = provider.Equals("OpenCode", StringComparison.OrdinalIgnoreCase);
        return GetBool(parameters, "batchShortlist", defaultEnabled);
    }

    private static int ResolveShortlistMax(IReadOnlyDictionary<string, string> parameters, int candidateCount)
    {
        var defaultMax = Math.Clamp((int)Math.Ceiling(candidateCount * 0.35), 2, 12);
        return Math.Clamp(GetInt(parameters, "shortlistMax", defaultMax), 0, candidateCount);
    }

    private static string ResolveTriageProvider(IReadOnlyDictionary<string, string> parameters, OperatorSettings settings) =>
        parameters.TryGetValue("triageProvider", out var provider) && !string.IsNullOrWhiteSpace(provider)
            ? TrimJsonString(provider)
            : settings.AiProvider;

    private static OperatorSettings ResolveTriageSettings(OperatorSettings settings, IReadOnlyDictionary<string, string> parameters)
    {
        var provider = ResolveTriageProvider(parameters, settings);
        if (provider.Equals(settings.AiProvider, StringComparison.OrdinalIgnoreCase))
            return settings;

        return new OperatorSettings
        {
            AiProvider = provider,
            AiModel = settings.AiModel,
            OpenCodeCliPath = settings.OpenCodeCliPath,
            PromptVersion = settings.PromptVersion,
            AiRetryCount = settings.AiRetryCount,
            AiTimeoutSeconds = settings.AiTimeoutSeconds,
            MaxAiCallsPerHour = settings.MaxAiCallsPerHour
        };
    }

    private static string BuildRunMessage(
        int fetched,
        int created,
        int skipped,
        ShortlistGate shortlist,
        int shortlistRejected)
    {
        var parts = new List<string> { $"Fetched {fetched} item(s)", $"{created} new lead(s)" };
        if (shortlist.Enabled)
            parts.Add($"shortlisted {shortlist.SelectedCount}/{shortlist.CandidateCount} via {shortlist.Provider}");
        if (shortlistRejected > 0)
            parts.Add($"recorded {shortlistRejected} shortlist reject(s)");
        if (skipped > 0)
            parts.Add($"skipped {skipped} without source URLs");
        return string.Join("; ", parts) + ".";
    }

    private IReadOnlyList<string> BuildTerms(SourceConfig source)
    {
        var terms = new List<string>();
        foreach (var name in PackNames(source))
            terms.AddRange(_queryPacks.GetTerms(name));
        return terms.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static string[] PackNames(SourceConfig source) =>
        source.QueryPacksCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private async Task<string> GetCampaignObjectiveAsync(SourceConfig source, CancellationToken ct)
    {
        if (source.CampaignId is not { } campaignId) return "";
        var campaign = await _db.Campaigns.AsNoTracking().FirstOrDefaultAsync(c => c.Id == campaignId, ct);
        return campaign?.Objective ?? "";
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
        catch { /* leave empty on malformed config */ }
        return dict;
    }

    private static bool GetBool(IReadOnlyDictionary<string, string> parameters, string key, bool fallback)
    {
        if (!parameters.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value))
            return fallback;
        return bool.TryParse(TrimJsonString(value), out var parsed) ? parsed : fallback;
    }

    private static int GetInt(IReadOnlyDictionary<string, string> parameters, string key, int fallback)
    {
        if (!parameters.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value))
            return fallback;
        return int.TryParse(TrimJsonString(value), out var parsed) ? parsed : fallback;
    }

    public async Task<ConnectorHealth> CheckHealthAsync(string sourceKey, CancellationToken ct)
    {
        var source = await _db.SourceConfigs.AsNoTracking().FirstOrDefaultAsync(s => s.SourceKey == sourceKey, ct);
        var parameters = source is null ? new Dictionary<string, string>() : ParseParameters(source.ParametersJson);
        var connectorKey = ResolveConnectorKey(sourceKey, parameters);
        var connector = _connectors.FirstOrDefault(c => c.SourceKey == connectorKey);
        if (connector is null) return new ConnectorHealth { Healthy = false, Message = "No connector registered." };
        return await connector.CheckHealthAsync(ct);
    }

    private static string ResolveConnectorKey(string sourceKey, IReadOnlyDictionary<string, string> parameters) =>
        parameters.TryGetValue("connector", out var connector) && !string.IsNullOrWhiteSpace(connector)
            ? connector.Trim()
            : sourceKey;

    private static string TrimJsonString(string value) => value.Trim().Trim('"');

    private static string Compact(string value, int max)
    {
        var compact = string.Join(' ', (value ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return compact.Length <= max ? compact : compact[..max];
    }

    private sealed class ShortlistGate
    {
        public static readonly ShortlistGate Disabled = new(false,
            new Dictionary<RawSourceItem, string>(), new HashSet<string>(), 0, "");

        public ShortlistGate(
            bool enabled,
            IReadOnlyDictionary<RawSourceItem, string> candidateIds,
            IReadOnlySet<string> selectedIds,
            int candidateCount,
            string provider)
        {
            Enabled = enabled;
            CandidateIds = candidateIds;
            SelectedIds = selectedIds;
            CandidateCount = candidateCount;
            Provider = provider;
        }

        public bool Enabled { get; }
        public IReadOnlyDictionary<RawSourceItem, string> CandidateIds { get; }
        public IReadOnlySet<string> SelectedIds { get; }
        public int CandidateCount { get; }
        public int SelectedCount => SelectedIds.Count;
        public string Provider { get; }

        public bool ShouldRecordRawOnly(RawSourceItem item) =>
            Enabled &&
            CandidateIds.TryGetValue(item, out var id) &&
            !SelectedIds.Contains(id);
    }
}
