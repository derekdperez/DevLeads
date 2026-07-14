namespace DevLeads.Core.Entities;

/// <summary>
/// One day's business-advisor briefing shown on the Today page: top priorities, pipeline
/// observations, and presence nudges. At most one per calendar day; written by AI when
/// available, otherwise by the deterministic fallback (Provider = "Heuristic").
/// </summary>
public class AdvisorBriefing
{
    public long Id { get; set; }

    /// <summary>Midnight UTC of the day this briefing covers.</summary>
    public DateTimeOffset ForDate { get; set; }

    public string BodyMarkdown { get; set; } = "";

    public string Provider { get; set; } = "";
    public string Model { get; set; } = "";

    public DateTimeOffset CreatedAt { get; set; }
}
