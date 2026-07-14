namespace DevLeads.Core.Entities;

/// <summary>
/// A dated reminder to touch a client or push an engagement forward. Due/overdue
/// follow-ups are the backbone of the Today page's "needs your attention" list.
/// </summary>
public class FollowUp
{
    public long Id { get; set; }

    public long ClientId { get; set; }
    public Client? Client { get; set; }

    /// <summary>Engagement this follow-up belongs to (null = relationship-level).</summary>
    public long? EngagementId { get; set; }

    /// <summary>What to do: "check if the quote landed", "ask how launch went"…</summary>
    public string Note { get; set; } = "";

    public DateTimeOffset DueAt { get; set; }

    public FollowUpStatus Status { get; set; } = FollowUpStatus.Pending;
    public DateTimeOffset? CompletedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
