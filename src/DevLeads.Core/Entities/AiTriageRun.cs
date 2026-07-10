namespace DevLeads.Core.Entities;

/// <summary>An auditable record of a single-pass structured AI triage call.</summary>
public class AiTriageRun
{
    public long Id { get; set; }
    public long OpportunityId { get; set; }

    public string Provider { get; set; } = "";
    public string Model { get; set; } = "";
    public string PromptVersion { get; set; } = "";
    public AiJobStatus Status { get; set; } = AiJobStatus.Queued;

    public string RequestJson { get; set; } = "{}";
    public string? ResponseJson { get; set; }
    public string? ErrorMessage { get; set; }

    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }

    public Opportunity? Opportunity { get; set; }
}
