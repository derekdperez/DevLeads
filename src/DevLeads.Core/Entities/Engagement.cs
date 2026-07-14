namespace DevLeads.Core.Entities;

/// <summary>
/// One bounded piece of work for a client: a fix, a project, or a retainer. Tracks the
/// commercial state (status, fee) and delivery expectations (due date, next deliverable)
/// so the Today page can surface deadlines before they slip.
/// </summary>
public class Engagement
{
    public long Id { get; set; }

    public long ClientId { get; set; }
    public Client? Client { get; set; }

    public string Title { get; set; } = "";
    public string Description { get; set; } = "";

    public EngagementStatus Status { get; set; } = EngagementStatus.Prospective;

    /// <summary>Agreed flat fee or rate note amount; null until negotiated.</summary>
    public double? AgreedFee { get; set; }

    /// <summary>The lead this engagement grew out of (null for direct work).</summary>
    public long? OpportunityId { get; set; }

    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? DueAt { get; set; }
    public DateTimeOffset? ClosedAt { get; set; }

    /// <summary>The single next thing owed to the client — keeps focus concrete.</summary>
    public string NextDeliverable { get; set; } = "";

    public string Notes { get; set; } = "";

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
