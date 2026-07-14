namespace DevLeads.Core.Entities;

/// <summary>
/// A human-reviewed response draft for activity on one of the operator's LinkedIn posts.
/// LinkedIn comment activity can be imported when the connected app has an approved read
/// scope; private inbox messages can be pasted manually because LinkedIn exposes no
/// general-purpose member inbox API.
/// </summary>
public class EngagementDraft
{
    public long Id { get; set; }
    public string Platform { get; set; } = "linkedin";
    public EngagementDraftKind Kind { get; set; } = EngagementDraftKind.CommentReply;

    /// <summary>Platform-native comment/message id; a local GUID for pasted messages.</summary>
    public string ExternalId { get; set; } = "";

    /// <summary>The tracked post whose discussion contains this engagement.</summary>
    public long? OperatorPostId { get; set; }
    public OperatorPost? Post { get; set; }

    /// <summary>LinkedIn post/share URN used as the social-action thread target.</summary>
    public string ThreadUrn { get; set; } = "";

    /// <summary>Composite comment URN when the response is a nested comment.</summary>
    public string ParentCommentUrn { get; set; } = "";
    public string AuthorUrn { get; set; } = "";
    public string AuthorName { get; set; } = "";
    public string SourceText { get; set; } = "";
    public string SourceUrl { get; set; } = "";
    public string DraftText { get; set; } = "";
    public EngagementDraftStatus Status { get; set; } = EngagementDraftStatus.PendingReview;
    public string Provider { get; set; } = "";
    public string Model { get; set; } = "";
    public string LastError { get; set; } = "";
    public DateTimeOffset ReceivedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }
}
