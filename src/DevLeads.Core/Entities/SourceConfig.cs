namespace DevLeads.Core.Entities;

/// <summary>Per-connector configuration and health, editable from the Sources page.</summary>
public class SourceConfig
{
    public long Id { get; set; }
    public string SourceKey { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public bool Enabled { get; set; } = true;

    /// <summary>Campaign whose objective this source feeds; leads inherit it at ingestion.</summary>
    public long? CampaignId { get; set; }

    public int PollIntervalMinutes { get; set; } = 15;
    public int MaxItemsPerRun { get; set; } = 25;

    /// <summary>Comma-separated query-pack names driving this source.</summary>
    public string QueryPacksCsv { get; set; } = "EmergencyGeneric,DotNetSqlPriority";

    /// <summary>Free-form connector parameters (subreddits, feed URLs, tags) as JSON.</summary>
    public string ParametersJson { get; set; } = "{}";

    public double MinPreFilterScore { get; set; } = 1;
    public double MinOpportunityScore { get; set; } = 40;
    public double DraftThreshold { get; set; } = 70;
    public double AlertThreshold { get; set; } = 85;

    public bool AutoModeEligible { get; set; }

    // Health tracking.
    public bool LastRunHealthy { get; set; } = true;
    public string? LastRunMessage { get; set; }
    public int LastRunItemCount { get; set; }
    public DateTimeOffset? LastRunAt { get; set; }
    public DateTimeOffset? NextRunAt { get; set; }
}
