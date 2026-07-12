namespace DevLeads.Core.Entities;

/// <summary>Point-in-time engagement reading for an operator post — the "learn" trail.</summary>
public class OperatorPostSnapshot
{
    public long Id { get; set; }
    public long OperatorPostId { get; set; }
    public DateTimeOffset At { get; set; }
    public int ReplyCount { get; set; }
    public int UpvoteCount { get; set; }
    public int ViewCount { get; set; }

    public OperatorPost? Post { get; set; }
}
