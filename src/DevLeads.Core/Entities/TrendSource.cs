namespace DevLeads.Core.Entities;

/// <summary>
/// A feed/community polled for *content* signals (trending topics, releases, updates)
/// rather than leads. Kept separate from SourceConfig on purpose: trend sources have no
/// triage thresholds, and their items become TrendSignals, never Opportunities.
/// </summary>
public class TrendSource
{
    public long Id { get; set; }

    /// <summary>Stable identifier used by the seeder and as the signals' SourceKey.</summary>
    public string SeedKey { get; set; } = "";

    /// <summary>Connector to fetch with: "rss", "hackernews", or "reddit".</summary>
    public string Kind { get; set; } = "rss";

    public string DisplayName { get; set; } = "";
    public string ParametersJson { get; set; } = "{}";
    public bool Enabled { get; set; } = true;

    /// <summary>Trend scanning is slow-moving: default twice a day.</summary>
    public int PollIntervalMinutes { get; set; } = 720;
    public int MaxItemsPerRun { get; set; } = 40;

    /// <summary>
    /// When true, only items matching the operator's skill profile are kept. Curated
    /// vendor/release feeds set false — the feed itself is already on-topic.
    /// </summary>
    public bool RequireSkillMatch { get; set; } = true;

    // Health tracking (mirrors SourceConfig so the UI can reuse its patterns).
    public bool LastRunHealthy { get; set; } = true;
    public string? LastRunMessage { get; set; }
    public int LastRunItemCount { get; set; }
    public DateTimeOffset? LastRunAt { get; set; }
    public DateTimeOffset? NextRunAt { get; set; }
}
