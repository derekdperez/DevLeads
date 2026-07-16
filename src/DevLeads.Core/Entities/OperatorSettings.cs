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
    public string WebAssetOutreachAiProvider { get; set; } = "";
    public string WebAssetOutreachAiModel { get; set; } = "";
    public string DiscordEngagementAiProvider { get; set; } = "";
    public string DiscordEngagementAiModel { get; set; } = "";
    public string CaseStudyAiProvider { get; set; } = "";
    public string CaseStudyAiModel { get; set; } = "";

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
            AiFeature.WebAssetOutreach => (WebAssetOutreachAiProvider, WebAssetOutreachAiModel),
            AiFeature.DiscordEngagement => (DiscordEngagementAiProvider, DiscordEngagementAiModel),
            AiFeature.CaseStudy => (CaseStudyAiProvider, CaseStudyAiModel),
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

    /// <summary>
    /// What the operator's LinkedIn profile says today, pasted as one plain-text block.
    /// The self-serve API cannot read profile sections, so this snapshot is the profile
    /// the action-plan review actually "views".
    /// </summary>
    public string LinkedInProfileSnapshot { get; set; } = "";

    // Discord bot integration. Everything runs through an official bot the operator
    // creates in the Developer Portal and invites to servers they're allowed to use —
    // never through the operator's personal account (self-bots violate Discord ToS).
    /// <summary>Bot token from the Discord Developer Portal (Bot → Token).</summary>
    public string DiscordBotToken { get; set; } = "";

    /// <summary>Application id from the Developer Portal — used to build the server invite URL.</summary>
    public string DiscordApplicationId { get; set; } = "";

    /// <summary>Bot user id/name, cached after the token is verified.</summary>
    public string DiscordBotUserId { get; set; } = "";
    public string DiscordBotName { get; set; } = "";

    /// <summary>
    /// The operator's OWN Discord user id (optional). When set, mentions of the operator
    /// in monitored channels are captured, and messages they post themselves are skipped.
    /// </summary>
    public string DiscordUserId { get; set; } = "";

    /// <summary>
    /// Search endpoint used by Site rescue discovery mode to find unknown broken sites from a
    /// probe's queries. Defaults to DuckDuckGo's keyless HTML endpoint; {q} is replaced with
    /// the URL-encoded query. Swap for another keyless HTML search if this gets rate-limited.
    /// </summary>
    public string WebScanSearchEndpoint { get; set; } = "https://html.duckduckgo.com/html/?q={q}";

    /// <summary>Max targets verified in a single Site rescue scan, to keep runs polite and bounded.</summary>
    public int WebScanMaxTargetsPerRun { get; set; } = 40;

    public int StaleItemMaxAgeHours { get; set; } = 72;
    public int FollowUpDefaultHours { get; set; } = 24;

    // Email (Gmail app password over SMTP/IMAP). Blank credentials disable the feature.
    /// <summary>Gmail address used to send/receive; blank falls back to ContactEmail.</summary>
    public string GmailAddress { get; set; } = "";

    /// <summary>Gmail app password (myaccount.google.com/apppasswords; requires 2FA). Spaces are stripped on use.</summary>
    public string GmailAppPassword { get; set; } = "";

    /// <summary>From display name; blank falls back to OperatorName.</summary>
    public string EmailSenderName { get; set; } = "";

    /// <summary>Appended to every outbound email. Must include a physical mailing address (CAN-SPAM).</summary>
    public string EmailSignature { get; set; } = "";

    /// <summary>Master enable for real SMTP sending; off = record-only behavior everywhere.</summary>
    public bool EmailSendEnabled { get; set; }

    /// <summary>Enables the IMAP inbox poll that imports replies into the unified inbox.</summary>
    public bool EmailInboxPollEnabled { get; set; }
    public int EmailInboxPollMinutes { get; set; } = 10;

    /// <summary>
    /// Required by every /api endpoint (X-Api-Key header or api_key query param), except
    /// the browser-driven OAuth redirects and the public resume download. Generated on
    /// first boot; regenerate from Settings.
    /// </summary>
    public string ApiKey { get; set; } = "";

    /// <summary>
    /// Public scheduling link (cal.com / Calendly). When set, outreach generation may
    /// offer it, email signatures include it, and the portfolio links it. Blank = off.
    /// </summary>
    public string BookingLink { get; set; } = "";

    // Portfolio site: a static site generated from operator data + published case studies
    // + published content drafts, deployed to a GitHub Pages repo with plain git.
    /// <summary>One-line professional headline shown at the top of the portfolio.</summary>
    public string Headline { get; set; } = "";

    /// <summary>Short third-or-first-person bio for the portfolio and reusable profiles.</summary>
    public string Bio { get; set; } = "";

    /// <summary>What the operator sells, in plain words — the portfolio "services" section.</summary>
    public string ServicesBlurb { get; set; } = "";

    /// <summary>Git remote the generated site is pushed to (e.g. git@github.com:user/user.github.io.git).</summary>
    public string PortfolioRepoUrl { get; set; } = "";
    public string PortfolioBranch { get; set; } = "main";

    /// <summary>Custom domain written to CNAME when set.</summary>
    public string PortfolioCname { get; set; } = "";

    /// <summary>Where the site is rendered; blank = App_Data/portfolio-site. Never wwwroot.</summary>
    public string PortfolioOutputDir { get; set; } = "";
    public DateTimeOffset? LastPortfolioDeployAt { get; set; }
    public string LastPortfolioDeployStatus { get; set; } = "";
}
