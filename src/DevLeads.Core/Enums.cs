namespace DevLeads.Core;

/// <summary>Workflow states an opportunity moves through from discovery to payment.</summary>
public enum OpportunityStatus
{
    New,
    PreFilteredRejected,
    AiQueued,
    AiTriaged,
    Rejected,
    NeedsReview,
    DraftReady,
    Approved,
    Contacted,
    Responded,
    Qualified,
    QuoteDrafted,
    QuoteSent,
    Accepted,
    InProgress,
    Fixed,
    PaymentPending,
    Paid,
    Won,
    Lost,
    FollowUpLater,
    DoNotContact
}

/// <summary>Priority band derived from the weighted opportunity score.</summary>
public enum Priority
{
    Low,
    Watch,
    Medium,
    High,
    Critical
}

/// <summary>Lifecycle of the single-pass AI triage job for an item.</summary>
public enum AiJobStatus
{
    NotRequired,
    Queued,
    Running,
    Succeeded,
    FailedRetryable,
    FailedFinal,
    NeedsManualReview
}

/// <summary>What the system recommends doing with an opportunity.</summary>
public enum OutreachRecommendation
{
    Ignore,
    Watch,
    ManualReview,
    DraftReply,
    DoNotContact
}

/// <summary>Outreach delivery mode for a given source/template/contact combination.</summary>
public enum OutreachMode
{
    HitlApproval,
    Auto,
    DraftOnly,
    ManualCopy,
    Disabled
}

/// <summary>Lifecycle of a single outreach attempt.</summary>
public enum OutreachStatus
{
    Draft,
    PendingApproval,
    Approved,
    Sent,
    Cancelled,
    Responded,
    Failed
}

/// <summary>Channel an outreach attempt is delivered over.</summary>
public enum OutreachChannel
{
    PublicReply,
    Email,
    DirectMessage,
    GmailDraft,
    ManualCopy
}

/// <summary>Payment lifecycle for a quote.</summary>
public enum QuoteStatus
{
    NotRequested,
    Drafted,
    Sent,
    PaymentPending,
    PaymentDueUponCompletion,
    Paid,
    Overdue,
    Refunded,
    Disputed,
    WrittenOff
}

/// <summary>Execution state for a hands-on work session.</summary>
public enum WorkSessionStatus
{
    NotStarted,
    InProgress,
    AwaitingClient,
    Completed,
    Blocked,
    Abandoned
}

/// <summary>How a contact was added to the suppression list.</summary>
public enum SuppressionContactType
{
    Email,
    Domain,
    Handle,
    ProfileUrl
}
