namespace DevLeads.Core.Entities;

/// <summary>Single-row settings for the solo operator: profile, AI, outreach, and safety controls.</summary>
public class OperatorSettings
{
    public long Id { get; set; } = 1;

    // Operator profile.
    public string OperatorName { get; set; } = "Derek Perez";
    public string BusinessName { get; set; } = "DevLeads";
    public string Location { get; set; } = "Florence, Massachusetts (Western MA)";
    public string ContactEmail { get; set; } = "derekdperez@gmail.com";
    public string RemoteAvailability { get; set; } = "Worldwide";
    public string CoreSkills { get; set; } = "ASP.NET Core, Blazor, IIS, SQL Server, Azure, APIs, production debugging";
    public string SecondarySkills { get; set; } = "Python, Node.js, React, Angular, PHP, Java, Go, mobile, WordPress, Shopify, Linux, cloud";
    public double MinimumFee { get; set; } = 100;
    public string PreferredPaymentTerms { get; set; } = "Due upon completion for bounded fixes";
    public bool EmergencyAvailability { get; set; } = true;

    // AI settings. Provider is switchable at runtime: "OpenCode" (default, local CLI),
    // "Codex" (OpenAI via the codex CLI), "Anthropic" (direct API), or "Heuristic"
    // (offline rules, always available).
    public string AiProvider { get; set; } = "OpenCode";

    /// <summary>Model for the selected provider ("provider/model" for OpenCode; model id for Codex/Anthropic).</summary>
    public string AiModel { get; set; } = DefaultOpenCodeModel;

    /// <summary>Path or command name of the opencode CLI binary.</summary>
    public string OpenCodeCliPath { get; set; } = "opencode";

    /// <summary>Path or command name of the codex CLI binary.</summary>
    public string CodexCliPath { get; set; } = "codex";

    // MiniMax M3 via the NVIDIA provider: free, fast enough, and reliably emits strict JSON.
    public const string DefaultOpenCodeModel = "nvidia/minimaxai/minimax-m3";
    public const string DefaultAnthropicModel = "claude-opus-4-8";
    public const string DefaultCodexModel = "gpt-5.6-sol";

    // Per-feature provider/model overrides. Empty string = inherit the global
    // AiProvider/AiModel pair above; resolution lives in AiFor/WithAiFor.
    public string TriageAiProvider { get; set; } = "";
    public string TriageAiModel { get; set; } = "";
    public string OutreachAiProvider { get; set; } = "";
    public string OutreachAiModel { get; set; } = "";
    public string ContentTopicsAiProvider { get; set; } = "";
    public string ContentTopicsAiModel { get; set; } = "";
    public string ContentDraftsAiProvider { get; set; } = "";
    public string ContentDraftsAiModel { get; set; } = "";
    public string PostDraftAiProvider { get; set; } = "";
    public string PostDraftAiModel { get; set; } = "";
    public string ThreadSummaryAiProvider { get; set; } = "";
    public string ThreadSummaryAiModel { get; set; } = "";
    // Optimization rewrites default to GPT-5.6 Sol via the codex CLI: rewrites need a
    // stronger writer than the triage default, and a stable model keeps experiment
    // variants comparable across runs.
    public string PostOptimizationAiProvider { get; set; } = "Codex";
    public string PostOptimizationAiModel { get; set; } = DefaultCodexModel;
    public string AdvisorAiProvider { get; set; } = "";
    public string AdvisorAiModel { get; set; } = "";
    public string PlatformDiscoveryAiProvider { get; set; } = "";
    public string PlatformDiscoveryAiModel { get; set; } = "";
    public string LinkedInEngagementAiProvider { get; set; } = "";
    public string LinkedInEngagementAiModel { get; set; } = "";
    // Profile review/rewrites default to the ChatGPT-backed codex CLI by request:
    // profile copy wants the strongest writer, and the volume is tiny (operator-initiated).
    public string LinkedInProfileAiProvider { get; set; } = "Codex";
    public string LinkedInProfileAiModel { get; set; } = DefaultCodexModel;

    /// <summary>The provider/model pair a feature actually uses, after override resolution.</summary>
    public (string Provider, string Model) AiFor(AiFeature feature)
    {
        var (p, m) = feature switch
        {
            AiFeature.Triage => (TriageAiProvider, TriageAiModel),
            AiFeature.Outreach => (OutreachAiProvider, OutreachAiModel),
            AiFeature.ContentTopics => (ContentTopicsAiProvider, ContentTopicsAiModel),
            AiFeature.ContentDrafts => (ContentDraftsAiProvider, ContentDraftsAiModel),
            AiFeature.PostDrafting => (PostDraftAiProvider, PostDraftAiModel),
            AiFeature.ThreadSummary => (ThreadSummaryAiProvider, ThreadSummaryAiModel),
            AiFeature.PostOptimization => (PostOptimizationAiProvider, PostOptimizationAiModel),
            AiFeature.AdvisorBriefing => (AdvisorAiProvider, AdvisorAiModel),
            AiFeature.PlatformDiscovery => (PlatformDiscoveryAiProvider, PlatformDiscoveryAiModel),
            AiFeature.LinkedInEngagement => (LinkedInEngagementAiProvider, LinkedInEngagementAiModel),
            AiFeature.LinkedInProfile => (LinkedInProfileAiProvider, LinkedInProfileAiModel),
            _ => ("", "")
        };
        var provider = string.IsNullOrWhiteSpace(p) ? AiProvider : p.Trim();
        // An overridden provider with no model gets that provider's default model —
        // the global AiModel almost certainly belongs to a different provider.
        var model = !string.IsNullOrWhiteSpace(m) ? m.Trim()
            : provider.Equals(AiProvider, StringComparison.OrdinalIgnoreCase) ? AiModel
            : DefaultModelFor(provider);
        return (provider, model);
    }

    /// <summary>Copy of these settings with AiProvider/AiModel resolved for a feature.</summary>
    public OperatorSettings WithAiFor(AiFeature feature)
    {
        var (provider, model) = AiFor(feature);
        var clone = (OperatorSettings)MemberwiseClone();
        clone.AiProvider = provider;
        clone.AiModel = model;
        return clone;
    }

    public static string DefaultModelFor(string provider) =>
        provider.Equals("Codex", StringComparison.OrdinalIgnoreCase) ? DefaultCodexModel
        : provider.Equals("Anthropic", StringComparison.OrdinalIgnoreCase) ? DefaultAnthropicModel
        : DefaultOpenCodeModel;
    public string PromptVersion { get; set; } = "v1";
    public int MaxAiCallsPerHour { get; set; } = 60;
    public double MaxAiSpendPerDay { get; set; } = 10;
    public double MinPreFilterScoreForAi { get; set; } = 1;
    public double MinAiConfidenceForDraft { get; set; } = 0.6;
    public double ManualReviewConfidenceThreshold { get; set; } = 0.5;
    public int AiRetryCount { get; set; } = 2;
    public int AiTimeoutSeconds { get; set; } = 120; // CLI models (reasoning) can take ~1 min per call

    // Outreach settings.
    public OutreachMode DefaultOutreachMode { get; set; } = OutreachMode.HitlApproval;
    public bool GlobalAutoModeEnabled { get; set; }
    public bool GlobalKillSwitch { get; set; } // when true, no outbound messages at all
    public int MaxSendsPerHour { get; set; } = 10;
    public int MaxSendsPerDay { get; set; } = 40;
    public double RequireApprovalAboveRisk { get; set; } = 0.3;
    public double RequireApprovalBelowConfidence { get; set; } = 0.8;
    public bool SuppressionListEnabled { get; set; } = true;
    public bool AuditLoggingEnabled { get; set; } = true;

    // Scoring thresholds.
    public double DraftScoreThreshold { get; set; } = 70;
    public double AlertScoreThreshold { get; set; } = 85;

    /// <summary>Campaign the UI is currently scoped to; null = combined view of all campaigns.</summary>
    public long? SelectedCampaignId { get; set; }

    // Discovery.
    public bool DiscoveryEnabled { get; set; } = true;

    /// <summary>Enables trend scanning + daily automatic topic suggestions for the content studio.</summary>
    public bool ContentDiscoveryEnabled { get; set; } = true;

    /// <summary>Reddit account whose submitted posts are synced into "My posts".</summary>
    public string RedditUsername { get; set; } = "Mission_Turn3102";

    // Reddit script-app credentials (reddit.com/prefs/apps → "script" type). With these,
    // "My posts" syncs accurate upvotes, reply counts, removal state, and author-only
    // view counts through the official API instead of the fragile anonymous RSS.
    public string RedditClientId { get; set; } = "";
    public string RedditClientSecret { get; set; } = "";
    public string RedditAppPassword { get; set; } = "";

    /// <summary>
    /// Private-feed token from reddit.com/prefs/feeds (the "feed=" value). Lets the app
    /// read the account's inbox (DMs + replies) anonymously as JSON, without OAuth.
    /// </summary>
    public string RedditInboxFeedToken { get; set; } = "7ff9c88db61f73b0ddc5162ab49eb0acf3f79f42";

    // LinkedIn OAuth + Community Management. The default scopes work with the
    // self-serve Sign In and Share products. Add r_member_social only after LinkedIn
    // grants that restricted permission; without it, posting works but comment sync does not.
    public string LinkedInClientId { get; set; } = "";
    public string LinkedInClientSecret { get; set; } = "";
    public string LinkedInRedirectUri { get; set; } = "";
    public string LinkedInScopes { get; set; } = "openid profile email w_member_social";
    public string LinkedInApiVersion { get; set; } = "202606";
    public string LinkedInAccessToken { get; set; } = "";
    public DateTimeOffset? LinkedInAccessTokenExpiresAt { get; set; }
    public string LinkedInRefreshToken { get; set; } = "";
    public DateTimeOffset? LinkedInRefreshTokenExpiresAt { get; set; }
    public string LinkedInMemberId { get; set; } = "";
    public string LinkedInMemberName { get; set; } = "";
    public string LinkedInMemberPictureUrl { get; set; } = "";
    public string LinkedInOAuthState { get; set; } = "";
    public DateTimeOffset? LinkedInOAuthStateExpiresAt { get; set; }

    /// <summary>The AI's latest overall LinkedIn-profile assessment: strengths, gaps, and next improvements.</summary>
    public string LinkedInProfileReview { get; set; } = "";
    public DateTimeOffset? LinkedInProfileReviewAt { get; set; }

    public int StaleItemMaxAgeHours { get; set; } = 72;
    public int FollowUpDefaultHours { get; set; } = 24;
}
