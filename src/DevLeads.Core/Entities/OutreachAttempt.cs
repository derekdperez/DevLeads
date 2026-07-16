namespace DevLeads.Core.Entities;

/// <summary>A drafted, approved, or sent outreach message tied to an opportunity.</summary>
public class OutreachAttempt
{
    public long Id { get; set; }
    public long OpportunityId { get; set; }

    public OutreachChannel Channel { get; set; } = OutreachChannel.ManualCopy;
    public OutreachMode Mode { get; set; } = OutreachMode.HitlApproval;

    public string? Subject { get; set; }
    public string Body { get; set; } = "";
    public string TemplateKey { get; set; } = "";

    /// <summary>Destination address when Channel is Email; empty for public replies and manual copy.</summary>
    public string RecipientEmail { get; set; } = "";

    /// <summary>RFC Message-Id of the delivered email, used to correlate inbound replies.</summary>
    public string SentMessageId { get; set; } = "";

    public OutreachStatus Status { get; set; } = OutreachStatus.Draft;
    public bool RequiresApproval { get; set; } = true;

    public DateTimeOffset? ApprovedAt { get; set; }
    public DateTimeOffset? SentAt { get; set; }
    public DateTimeOffset? ResponseReceivedAt { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public Opportunity? Opportunity { get; set; }
}
