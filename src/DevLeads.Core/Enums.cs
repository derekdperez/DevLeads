namespace DevLeads.Core;

/// <summary>Workflow states an opportunity moves through from discovery to payment.</summary>
public enum OpportunityStatus
{
    New,
    PreFilteredRejected,
    AiQueued,
    AiTriaged,

    /// <summary>
    /// Actionable candidate that reached 50–99% of its source's required opportunity
    /// score. Retained for scoring review, but excluded from normal lead/outreach views.
    /// </summary>
    JustMissed,

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
    DoNotContact,

    /// <summary>Operator dismissed the lead: hidden from every view, never purged, never re-ingested.</summary>
    Archived
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
    /// <summary>Waiting in the AI generation queue — body is written by the next batched generation run.</summary>
    QueuedForGeneration,
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

/// <summary>Lifecycle of an AI-suggested publishing topic.</summary>
public enum ContentTopicStatus
{
    Suggested,
    Drafted,
    Dismissed
}

/// <summary>Lifecycle of a generated content draft.</summary>
public enum ContentDraftStatus
{
    Draft,
    Final,
    Published,
    Discarded
}

/// <summary>Publishable formats the content studio can generate.</summary>
public enum ContentFormat
{
    BlogPost,
    Article,
    WhitePaper,
    ResearchPaper,
    LinkedInPost
}

/// <summary>Lifecycle of one of the operator's own posts on an external platform.</summary>
public enum OperatorPostStatus
{
    Draft,
    Active,
    Expired,
    Removed,
    Archived
}

/// <summary>Lifecycle of an AI-proposed rewrite of one of the operator's posts.</summary>
public enum OperatorPostRevisionStatus
{
    /// <summary>Generated, waiting for the operator to post it on the platform.</summary>
    Proposed,
    /// <summary>Live on the platform; the baseline is frozen and the experiment is running.</summary>
    Applied,
    Dismissed
}

/// <summary>What kind of inbox item a received operator message is.</summary>
public enum OperatorMessageKind
{
    /// <summary>A true DM / private message (reddit t4).</summary>
    PrivateMessage,
    /// <summary>A reply to one of the operator's comments (reddit t1, was_comment).</summary>
    CommentReply,
    /// <summary>A top-level reply on one of the operator's posts.</summary>
    PostReply,
    /// <summary>A username mention.</summary>
    Mention,
    /// <summary>An email received in the connected Gmail inbox.</summary>
    Email,
    Other
}

/// <summary>Operator-side lifecycle of a received message.</summary>
public enum OperatorMessageStatus
{
    Unread,
    Read,
    Replied,
    Archived
}

/// <summary>The LinkedIn interaction an engagement draft responds to.</summary>
public enum EngagementDraftKind
{
    CommentReply,
    PrivateMessageReply,
    MentionReply,
    Other
}

/// <summary>Human-in-the-loop lifecycle for a LinkedIn engagement response.</summary>
public enum EngagementDraftStatus
{
    PendingReview,
    Published,
    Dismissed,
    Failed
}

/// <summary>Which part of LinkedIn presence-building a planned action improves.</summary>
public enum LinkedInActionCategory
{
    /// <summary>Improving the profile itself (headline, about, featured, photo, …).</summary>
    Profile,
    /// <summary>Finding and inviting relevant new connections.</summary>
    Connections,
    /// <summary>Talking with connections: messages, comment replies, follow-ups.</summary>
    Communication,
    /// <summary>Finding and pursuing paid-work opportunities.</summary>
    Opportunities,
    /// <summary>Producing professional, valuable content.</summary>
    Content,
    /// <summary>Earning credibility and trust (recommendations, proof, consistency).</summary>
    Credibility,
    /// <summary>Providing value to others with no immediate ask.</summary>
    GiveValue
}

/// <summary>Operator-side lifecycle of one planned LinkedIn action.</summary>
public enum LinkedInActionStatus
{
    Pending,
    Done,
    Dismissed
}

/// <summary>Relationship stage of a client (a real person/business the operator works with).</summary>
public enum ClientStatus
{
    /// <summary>Promising contact — no agreed work yet.</summary>
    Prospect,
    /// <summary>Has active or recently agreed work.</summary>
    Active,
    /// <summary>Went quiet; worth a periodic check-in.</summary>
    Dormant,
    /// <summary>Finished relationship kept for history/referrals.</summary>
    Past
}

/// <summary>Lifecycle of a client engagement (a bounded project, fix, or retainer).</summary>
public enum EngagementStatus
{
    Prospective,
    Negotiating,
    Active,
    OnHold,
    Delivered,
    Closed,
    Lost
}

/// <summary>Lifecycle of a scheduled follow-up reminder.</summary>
public enum FollowUpStatus
{
    Pending,
    Done,
    Dismissed
}

/// <summary>Direction of a logged client interaction.</summary>
public enum InteractionDirection
{
    Inbound,
    Outbound
}

/// <summary>Where a platform sits in the operator's presence-building funnel.</summary>
public enum PlatformPresenceStatus
{
    /// <summary>Catalog/AI suggestion the operator has not acted on.</summary>
    Suggested,
    /// <summary>Operator decided to join but has not created the account yet.</summary>
    Planned,
    /// <summary>Account exists; posts/messages there are tracked.</summary>
    Active,
    /// <summary>Operator decided it is not worth the effort.</summary>
    Dismissed
}

/// <summary>How a contact was added to the suppression list.</summary>
public enum SuppressionContactType
{
    Email,
    Domain,
    Handle,
    ProfileUrl
}

/// <summary>How badly a scanned business web asset is broken.</summary>
public enum WebAssetSeverity
{
    /// <summary>Down: the page/site fails to serve (5xx, connection/TLS failure, hard error page).</summary>
    Down,
    /// <summary>Degraded: it loads but shows software errors, warnings, or partial breakage.</summary>
    Degraded,
    /// <summary>Warning: a softer problem worth flagging (expiring TLS, deprecation notices).</summary>
    Warning
}

/// <summary>Operator-side lifecycle of a discovered broken web asset (a potential repair lead).</summary>
public enum WebAssetStatus
{
    New,
    Reviewing,
    Contacted,
    Responded,
    Won,
    Dismissed
}

/// <summary>How a broken web asset was found.</summary>
public enum WebAssetDetection
{
    /// <summary>The operator supplied the domain/URL directly.</summary>
    ManualTarget,
    /// <summary>Surfaced by a search-engine query for the probe's error signatures.</summary>
    Discovery
}

/// <summary>Lifecycle of a publishable case study.</summary>
public enum CaseStudyStatus
{
    /// <summary>AI/operator draft — not shown anywhere public.</summary>
    Draft,
    /// <summary>Reviewed and factually correct, but not yet on the portfolio.</summary>
    Approved,
    /// <summary>Rendered onto the generated portfolio site.</summary>
    Published
}

/// <summary>
/// The distinct AI call sites in the app. Each can carry its own provider/model override
/// in <see cref="Entities.OperatorSettings"/>; an unset override inherits the global
/// AiProvider/AiModel pair. Not persisted — used only to key override resolution.
/// </summary>
public enum AiFeature
{
    /// <summary>Lead shortlist + single/batch triage (the discovery pipeline).</summary>
    Triage,
    /// <summary>Batched outreach-response generation for queued leads.</summary>
    Outreach,
    /// <summary>Content-studio topic suggestions from trend signals.</summary>
    ContentTopics,
    /// <summary>Content-studio long-form draft writing.</summary>
    ContentDrafts,
    /// <summary>Drafting the operator's own platform posts (My posts).</summary>
    PostDrafting,
    /// <summary>Summarizing the reply thread on one of the operator's posts.</summary>
    ThreadSummary,
    /// <summary>Post-optimization experiment rewrites (My posts).</summary>
    PostOptimization,
    /// <summary>The daily business-advisor briefing on the Today page.</summary>
    AdvisorBriefing,
    /// <summary>Suggesting new platforms to build a presence on (My posts).</summary>
    PlatformDiscovery,
    /// <summary>Batched LinkedIn comment/message response drafting.</summary>
    LinkedInEngagement,
    /// <summary>Reviewing the operator's LinkedIn presence and planning the next actions.</summary>
    LinkedInProfile,
    /// <summary>Batched repair-offer email drafting for discovered broken web assets (Site rescue).</summary>
    WebAssetOutreach,
    /// <summary>Batched Discord reply drafting for replies, mentions, and pasted DMs.</summary>
    DiscordEngagement,
    /// <summary>Case-study + testimonial-request drafting from delivered work (portfolio).</summary>
    CaseStudy
}
