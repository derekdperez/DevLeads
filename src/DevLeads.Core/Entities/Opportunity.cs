namespace DevLeads.Core.Entities;

/// <summary>
/// A triaged, scored emergency-repair lead. The central aggregate the whole app revolves around.
/// </summary>
public class Opportunity
{
    public long Id { get; set; }

    public string Title { get; set; } = "";
    public string Summary { get; set; } = "";
    public string SourceKey { get; set; } = "";
    public string SourceUrl { get; set; } = "";
    public string? AuthorName { get; set; }
    public string? AuthorProfileUrl { get; set; }

    public OpportunityStatus Status { get; set; } = OpportunityStatus.New;
    public Priority Priority { get; set; } = Priority.Low;

    // Weighted score components (0-100 each; Score is the blended total).
    public double Score { get; set; }
    public double UrgencyScore { get; set; }
    public double StackFitScore { get; set; }
    public double BusinessValueScore { get; set; }
    public double ReachabilityScore { get; set; }
    public double CompetitionScore { get; set; }
    public double TrustScore { get; set; }

    // AI triage output.
    public string ProblemType { get; set; } = "";
    /// <summary>"Explicit", "Implied", or "None"; "" when triage predates the field.</summary>
    public string PaymentIntent { get; set; } = "";

    /// <summary>Poster wants hands-on help (vs. information only); null when untriaged.</summary>
    public bool? AssistanceRequested { get; set; }
    public string DetectedStackJson { get; set; } = "[]";
    public string SuggestedFirstStep { get; set; } = "";
    public string EstimatedCause { get; set; } = "";

    public double? EstimatedFeeMin { get; set; }
    public double? EstimatedFeeMax { get; set; }

    /// <summary>
    /// True when the fee is a category-based suggestion; false when the poster explicitly
    /// stated the amount (bounty value, "Reward: $15", stated budget) — shown as fact.
    /// </summary>
    public bool FeeIsEstimate { get; set; } = true;
    public int? EstimatedFixMinutesMin { get; set; }
    public int? EstimatedFixMinutesMax { get; set; }

    public double AiConfidence { get; set; }
    public OutreachRecommendation OutreachRecommendation { get; set; } = OutreachRecommendation.ManualReview;
    public string? RejectionReason { get; set; }

    public AiJobStatus AiJobStatus { get; set; } = AiJobStatus.NotRequired;

    // Heuristic pre-filter output.
    public double HeuristicScore { get; set; }
    public string MatchedTermsJson { get; set; } = "[]";
    public string? PreFilterRejectReason { get; set; }

    // Auto-mode eligibility (defaults to human-in-the-loop).
    public bool AutoEligible { get; set; }

    public DateTimeOffset PostedAt { get; set; }
    public DateTimeOffset FirstSeenAt { get; set; }
    public DateTimeOffset LastSeenAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    // Follow-up scheduling.
    public DateTimeOffset? NextFollowUpAt { get; set; }
    public string? WorkNotes { get; set; }

    // Navigation.
    public List<AiTriageRun> TriageRuns { get; set; } = new();
    public List<OutreachAttempt> OutreachAttempts { get; set; } = new();
    public List<Quote> Quotes { get; set; } = new();
    public List<WorkSession> WorkSessions { get; set; } = new();
}
