namespace DevLeads.Core.Entities;

/// <summary>
/// A publishable success story distilled from delivered work: the problem, what was done,
/// and the outcome, optionally with a client testimonial. Drafted by AI from the
/// engagement/work-session record, edited and published by the operator, and rendered
/// onto the generated portfolio site. Client names appear only with explicit consent;
/// anonymized studies never identify the client.
/// </summary>
public class CaseStudy
{
    public long Id { get; set; }

    public string Title { get; set; } = "";

    /// <summary>URL-safe unique slug for the portfolio page (case-studies/{slug}.html).</summary>
    public string Slug { get; set; } = "";

    public string ProblemSummary { get; set; } = "";
    public string SolutionSummary { get; set; } = "";
    public string OutcomeSummary { get; set; } = "";

    /// <summary>Comma-separated technologies involved, shown as tags.</summary>
    public string Technologies { get; set; } = "";

    public string TestimonialQuote { get; set; } = "";
    public string TestimonialAttribution { get; set; } = "";

    /// <summary>AI-drafted message asking the client for a testimonial — sent manually.</summary>
    public string TestimonialRequestDraft { get; set; } = "";

    /// <summary>The client agreed to be named/quoted publicly. Without it, publish anonymized.</summary>
    public bool ClientConsent { get; set; }

    /// <summary>Render without any client-identifying details ("a small e-commerce business").</summary>
    public bool Anonymized { get; set; } = true;

    public CaseStudyStatus Status { get; set; } = CaseStudyStatus.Draft;

    // Where the story came from (all optional — manual case studies are allowed).
    public long? OpportunityId { get; set; }
    public long? EngagementId { get; set; }
    public long? WorkSessionId { get; set; }

    public string Provider { get; set; } = "";
    public string Model { get; set; } = "";
    public DateTimeOffset? GeneratedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
