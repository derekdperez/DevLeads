namespace DevLeads.Core.Entities;

/// <summary>A flat-fee emergency-repair quote and its payment lifecycle.</summary>
public class Quote
{
    public long Id { get; set; }
    public long OpportunityId { get; set; }

    public double Amount { get; set; }
    public bool PaymentDueUponCompletion { get; set; } = true;
    public double? DiagnosticFee { get; set; }

    public string Scope { get; set; } = "";
    public string Exclusions { get; set; } = "";
    public string? PaymentUrl { get; set; }

    public QuoteStatus Status { get; set; } = QuoteStatus.Drafted;

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? SentAt { get; set; }
    public DateTimeOffset? PaidAt { get; set; }
    public DateTimeOffset? DueAt { get; set; }

    public Opportunity? Opportunity { get; set; }
}
