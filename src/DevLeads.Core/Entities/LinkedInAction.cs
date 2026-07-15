namespace DevLeads.Core.Entities;

/// <summary>
/// One concrete step in the AI-planned LinkedIn action plan (improve the profile, invite
/// connections, publish content, …). LinkedIn's self-serve API cannot automate any of
/// this, so every action is hand-executed by the operator and then marked done; Done and
/// Dismissed rows survive plan regeneration and are fed back so new plans build forward.
/// </summary>
public class LinkedInAction
{
    public long Id { get; set; }

    public LinkedInActionCategory Category { get; set; }

    /// <summary>Short imperative name of the action.</summary>
    public string Title { get; set; } = "";

    /// <summary>Why this action matters for this operator right now.</summary>
    public string Why { get; set; } = "";

    /// <summary>Newline-separated concrete steps, in execution order.</summary>
    public string Steps { get; set; } = "";

    public LinkedInActionStatus Status { get; set; } = LinkedInActionStatus.Pending;

    /// <summary>Position inside the generated plan (AI impact order).</summary>
    public int SortOrder { get; set; }

    public string Provider { get; set; } = "";
    public string Model { get; set; } = "";
    public DateTimeOffset GeneratedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
