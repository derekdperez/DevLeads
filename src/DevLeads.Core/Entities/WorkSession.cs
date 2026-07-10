namespace DevLeads.Core.Entities;

/// <summary>Tracks execution once a lead becomes real work: checklist, notes, fix summary.</summary>
public class WorkSession
{
    public long Id { get; set; }
    public long OpportunityId { get; set; }

    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? EndedAt { get; set; }
    public WorkSessionStatus Status { get; set; } = WorkSessionStatus.NotStarted;

    /// <summary>JSON array of { item, checked } for the emergency checklist.</summary>
    public string AccessChecklistJson { get; set; } = "[]";
    public string Notes { get; set; } = "";
    public string FixSummary { get; set; } = "";
    public string ClientConfirmation { get; set; } = "";

    public Opportunity? Opportunity { get; set; }
}
