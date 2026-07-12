namespace DevLeads.Core.Entities;

/// <summary>
/// A lead-generation campaign: a named objective (e.g. emergency rescue work, .NET legacy
/// modernization consulting) that owns a set of sources and the leads they produce. The
/// objective text is injected into AI triage so relevance is judged per campaign.
/// </summary>
public class Campaign
{
    public long Id { get; set; }

    /// <summary>Stable identifier used by the seeder (never shown as the primary label).</summary>
    public string Key { get; set; } = "";

    public string Name { get; set; } = "";
    public string Emoji { get; set; } = "🎯";

    /// <summary>
    /// Operator-defined objective describing what a qualifying lead looks like.
    /// Passed verbatim to AI triage as the relevance bar for this campaign's sources.
    /// </summary>
    public string Objective { get; set; } = "";

    /// <summary>Disabled campaigns keep their data but their sources stop polling.</summary>
    public bool Enabled { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; }
}
