namespace DevLeads.Core.Entities;

/// <summary>
/// A private message or reply RECEIVED by the operator on an external platform (a reddit
/// DM, a comment reply on one of their [For Hire] posts, an Upwork message…). These are
/// the hottest signal in the app — someone reached out directly — so they are tracked
/// next to the operator's own posts. Reddit syncs automatically; other platforms are
/// manual entries until they grow connectors.
/// </summary>
public class OperatorMessage
{
    public long Id { get; set; }

    /// <summary>"reddit", "upwork", "craigslist", "linkedin", "other".</summary>
    public string Platform { get; set; } = "reddit";

    /// <summary>Platform-native fullname (reddit t4_/t1_ id); a GUID for manual entries.</summary>
    public string ExternalId { get; set; } = "";

    public OperatorMessageKind Kind { get; set; } = OperatorMessageKind.PrivateMessage;

    /// <summary>Sender's platform handle.</summary>
    public string Author { get; set; } = "";

    public string Subject { get; set; } = "";
    public string Body { get; set; } = "";

    /// <summary>Where the conversation lives: subreddit for replies, empty for pure DMs.</summary>
    public string Community { get; set; } = "";

    /// <summary>Link to the message/reply on the platform, where one exists.</summary>
    public string Url { get; set; } = "";

    public OperatorMessageStatus Status { get; set; } = OperatorMessageStatus.Unread;

    /// <summary>The tracked post this reply belongs to, when it can be matched (null for DMs).</summary>
    public long? OperatorPostId { get; set; }
    public OperatorPost? Post { get; set; }

    /// <summary>Operator notes: who this is, outcome, follow-up plan.</summary>
    public string Notes { get; set; } = "";

    public DateTimeOffset ReceivedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
