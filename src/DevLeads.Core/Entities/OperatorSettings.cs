namespace DevLeads.Core.Entities;

/// <summary>Single-row settings for the solo operator: profile, AI, outreach, and safety controls.</summary>
public class OperatorSettings
{
    public long Id { get; set; } = 1;

    // Operator profile.
    public string OperatorName { get; set; } = "Senior Engineer";
    public string BusinessName { get; set; } = "DevLeads";
    public string Location { get; set; } = "Massachusetts";
    public string RemoteAvailability { get; set; } = "Worldwide";
    public string CoreSkills { get; set; } = "ASP.NET Core, Blazor, IIS, SQL Server, Azure, APIs, production debugging";
    public string SecondarySkills { get; set; } = "IIS, Windows Server, DNS, TLS, hosting, SQL performance tuning";
    public double MinimumFee { get; set; } = 100;
    public string PreferredPaymentTerms { get; set; } = "Due upon completion for bounded fixes";
    public bool EmergencyAvailability { get; set; } = true;

    // AI settings. Provider is switchable at runtime: "OpenCode" (default, local CLI),
    // "Anthropic" (direct API), or "Heuristic" (offline rules, always available).
    public string AiProvider { get; set; } = "OpenCode";

    /// <summary>Model for the selected provider ("provider/model" for OpenCode; model id for Anthropic).</summary>
    public string AiModel { get; set; } = DefaultOpenCodeModel;

    /// <summary>Path or command name of the opencode CLI binary.</summary>
    public string OpenCodeCliPath { get; set; } = "opencode";

    // MiniMax M3 via the NVIDIA provider: free, fast enough, and reliably emits strict JSON.
    public const string DefaultOpenCodeModel = "nvidia/minimaxai/minimax-m3";
    public const string DefaultAnthropicModel = "claude-opus-4-8";
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
    public int StaleItemMaxAgeHours { get; set; } = 72;
    public int FollowUpDefaultHours { get; set; } = 24;
}
