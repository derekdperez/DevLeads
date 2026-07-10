namespace DevLeads.Core.Entities;

/// <summary>
/// A normalized public item fetched from a source connector, stored before/after triage.
/// Also serves as the connector output DTO.
/// </summary>
public class RawSourceItem
{
    public long Id { get; set; }
    public long? OpportunityId { get; set; }

    public string SourceKey { get; set; } = "";
    public string ExternalId { get; set; } = "";
    public string Url { get; set; } = "";
    public string? AuthorName { get; set; }
    public string? AuthorProfileUrl { get; set; }
    public string Title { get; set; } = "";
    public string BodyText { get; set; } = "";

    public DateTimeOffset PostedAt { get; set; }
    public DateTimeOffset FetchedAt { get; set; }

    public string RawJson { get; set; } = "{}";

    /// <summary>Stable hash of normalized content for duplicate detection.</summary>
    public string ContentHash { get; set; } = "";

    public Opportunity? Opportunity { get; set; }
}
