namespace DevLeads.Core.Entities;

/// <summary>
/// One of the operator's OWN posts on an external platform (a [For Hire] reddit post, an
/// Upwork profile/proposal, a Craigslist ad…). Tracked so the operator can see what is
/// getting replies and learn which platforms/wording work. Reddit posts sync
/// automatically; other platforms are maintained by hand until they grow connectors.
/// </summary>
public class OperatorPost
{
    public long Id { get; set; }

    /// <summary>"reddit", "upwork", "craigslist", "monster", "indeed", "linkedin", "other".</summary>
    public string Platform { get; set; } = "other";

    /// <summary>Platform-native id (reddit t3 id); a GUID for manual entries.</summary>
    public string ExternalId { get; set; } = "";

    public string Url { get; set; } = "";
    public string Title { get; set; } = "";

    /// <summary>The post text itself, kept so old wording can be compared against results.</summary>
    public string Body { get; set; } = "";

    /// <summary>Subreddit / board / category the post lives in.</summary>
    public string Community { get; set; } = "";

    public OperatorPostStatus Status { get; set; } = OperatorPostStatus.Active;

    /// <summary>Campaign this post advertises (null = general).</summary>
    public long? CampaignId { get; set; }

    /// <summary>Replies/comments last observed on the platform.</summary>
    public int ReplyCount { get; set; }

    /// <summary>Upvotes/score last observed (requires the authenticated reddit API).</summary>
    public int UpvoteCount { get; set; }

    /// <summary>Views last reported by the platform or explicitly entered by the operator.</summary>
    public int ViewCount { get; set; }

    /// <summary>
    /// Distinguishes an observed count of zero from a platform that did not expose a
    /// view count. Reddit commonly omits this value even from authenticated listings.
    /// </summary>
    public bool ViewCountKnown { get; set; }

    /// <summary>AI summary of the thread (main points + suggested way forward as OP).</summary>
    public string ThreadSummary { get; set; } = "";
    public DateTimeOffset? SummarizedAt { get; set; }

    public DateTimeOffset? LastCheckedAt { get; set; }

    /// <summary>Operator notes: outcomes, learnings, what to change next time.</summary>
    public string Notes { get; set; } = "";

    /// <summary>
    /// Requested publish time for an approved LinkedIn draft. The worker publishes only
    /// Draft rows whose schedule is due; null means no automatic publishing.
    /// </summary>
    public DateTimeOffset? ScheduledAt { get; set; }

    public DateTimeOffset PostedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public List<OperatorPostSnapshot> Snapshots { get; set; } = new();
}
