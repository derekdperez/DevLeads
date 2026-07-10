namespace DevLeads.Core.Entities;

/// <summary>A contact that must never be messaged (opt-out, complaint, or manual block).</summary>
public class SuppressionEntry
{
    public long Id { get; set; }
    public string ContactValue { get; set; } = "";
    public SuppressionContactType ContactType { get; set; }
    public string Reason { get; set; } = "";
    public string Source { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
}
