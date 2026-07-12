namespace DevLeads.Core.Entities;

/// <summary>
/// One AI-proposed (or operator-made) rewrite of a tracked post — the experiment unit
/// for post optimization. Generated as a Proposed variant with a named approach; when
/// the operator applies it on the platform, the pre-change engagement baseline is
/// frozen here so before/after views-per-day can be compared honestly. A title change
/// on reddit requires delete+repost, so applying one spawns a NEW tracked post
/// (ResultPostId) and expires the original.
/// </summary>
public class OperatorPostRevision
{
    public long Id { get; set; }

    /// <summary>The post this rewrite targets (the "before").</summary>
    public long OperatorPostId { get; set; }
    public OperatorPost? Post { get; set; }

    /// <summary>Short strategy label, e.g. "outcome-led title" or "niche specialist".</summary>
    public string Approach { get; set; } = "";

    /// <summary>What is different from the original and why it might perform better.</summary>
    public string Rationale { get; set; } = "";

    public string OldTitle { get; set; } = "";
    public string OldBody { get; set; } = "";
    public string NewTitle { get; set; } = "";
    public string NewBody { get; set; } = "";

    public OperatorPostRevisionStatus Status { get; set; } = OperatorPostRevisionStatus.Proposed;

    public string Provider { get; set; } = "";
    public string Model { get; set; } = "";

    // Engagement frozen at apply time — the "before" side of the experiment.
    public int BaselineViewCount { get; set; }
    public int BaselineReplyCount { get; set; }
    public int BaselineUpvoteCount { get; set; }

    /// <summary>Views/day over the post's life before the change (rate, so reposts compare fairly).</summary>
    public double BaselineViewsPerDay { get; set; }
    public double BaselineRepliesPerDay { get; set; }

    /// <summary>When a title change forced a repost: the NEW tracked post carrying the rewrite.</summary>
    public long? ResultPostId { get; set; }

    /// <summary>Operator outcome notes: what we learned from this experiment.</summary>
    public string Notes { get; set; } = "";

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? AppliedAt { get; set; }
}
