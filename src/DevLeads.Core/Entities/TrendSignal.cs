namespace DevLeads.Core.Entities;

/// <summary>
/// One piece of evidence that something is trending: a hot post, a release note, an
/// announcement. Signals feed AI topic generation and are pruned after ~30 days.
/// </summary>
public class TrendSignal
{
    public long Id { get; set; }

    /// <summary>The TrendSource SeedKey that produced this signal.</summary>
    public string SourceKey { get; set; } = "";
    public string ExternalId { get; set; } = "";

    public string Url { get; set; } = "";
    public string Title { get; set; } = "";
    public string Snippet { get; set; } = "";

    public DateTimeOffset PostedAt { get; set; }
    public DateTimeOffset FetchedAt { get; set; }

    /// <summary>Points/comments where the platform exposes them (HN); 0 otherwise.</summary>
    public double Engagement { get; set; }

    /// <summary>Operator skills matched in the text, as a JSON string list.</summary>
    public string MatchedSkillsJson { get; set; } = "[]";

    /// <summary>Blended skill-match + engagement + recency score used to rank signals.</summary>
    public double Hotness { get; set; }

    /// <summary>Set once the signal has been cited as evidence for a generated topic.</summary>
    public bool UsedInTopic { get; set; }
}
