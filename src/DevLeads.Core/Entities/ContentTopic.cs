namespace DevLeads.Core.Entities;

/// <summary>
/// An AI-suggested publishing topic distilled from trend signals: what to write about,
/// the specific angle, and why an audience would care right now.
/// </summary>
public class ContentTopic
{
    public long Id { get; set; }

    public string Title { get; set; } = "";

    /// <summary>The specific take that differentiates the piece from generic coverage.</summary>
    public string Angle { get; set; } = "";

    /// <summary>Why this will interest readers now (trend evidence, timing).</summary>
    public string Rationale { get; set; } = "";

    /// <summary>Projected audience interest, 0–100.</summary>
    public double InterestScore { get; set; }

    /// <summary>Operator skills the topic showcases, as a JSON string list.</summary>
    public string SkillsJson { get; set; } = "[]";

    /// <summary>Supporting signals as JSON [{"title":…,"url":…}] — cited in drafts.</summary>
    public string EvidenceJson { get; set; } = "[]";

    /// <summary>AI-suggested formats (comma-separated ContentFormat names).</summary>
    public string SuggestedFormatsCsv { get; set; } = "";

    public ContentTopicStatus Status { get; set; } = ContentTopicStatus.Suggested;

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public List<ContentDraft> Drafts { get; set; } = new();
}
