using DevLeads.Core.Skills;

namespace DevLeads.Core.Scoring;

/// <summary>The blended score plus its weighted components.</summary>
public sealed class ScoreBreakdown
{
    public double UrgencyScore { get; set; }
    public double StackFitScore { get; set; }
    public double BusinessValueScore { get; set; }
    public double ReachabilityScore { get; set; }
    public double CompetitionScore { get; set; }
    public double TrustScore { get; set; }
    /// <summary>Points removed when the original post is not English.</summary>
    public double LanguagePenalty { get; set; }
    public double Total { get; set; }
    public Priority Priority { get; set; }
}

/// <summary>Inputs the scorer needs, decoupled from persistence.</summary>
public sealed class ScoringInput
{
    public AiTriageResult? Ai { get; set; }
    public PreFilterResult? PreFilter { get; set; }
    public string SourceKey { get; set; } = "";
    public DateTimeOffset PostedAt { get; set; }
    public bool RedFlagged { get; set; }
    public bool HasContact { get; set; }

    /// <summary>Predominant language of the original post; non-English adds response friction.</summary>
    public string LanguageCode { get; set; } = "en";

    /// <summary>
    /// Skills from the operator profile that matched this lead's text/stack.
    /// Null when no skill profile exists (falls back to the built-in stack lists).
    /// </summary>
    public IReadOnlyList<SkillMatch>? SkillMatches { get; set; }

    /// <summary>Compensation the poster explicitly stated (upper bound); null when none stated.</summary>
    public double? OfferedAmount { get; set; }

    /// <summary>Someone else already claimed the work (assigned issue, PR in flight).</summary>
    public bool ClaimedByOthers { get; set; }

    /// <summary>Others already engaging with the post (issue comments, thread participants).</summary>
    public int CompetingResponses { get; set; }

    /// <summary>
    /// Primary-stack demands outside the operator's profile (Go, Python, Java…) found in
    /// the post — see <see cref="SkillMatcher.ForeignStackDemands"/>.
    /// </summary>
    public IReadOnlyList<string> ForeignStackDemands { get; set; } = Array.Empty<string>();
}

/// <summary>
/// Blends heuristic, AI, source-reputation, recency, stack-fit, business-value,
/// reachability and trust signals into a single weighted opportunity score.
/// </summary>
public static class OpportunityScorer
{
    // Weights from the design document.
    private const double WUrgency = 0.25, WStack = 0.20, WBusiness = 0.20, WReach = 0.15, WCompetition = 0.10, WTrust = 0.10;
    public const double NonEnglishPenalty = 8;

    private static readonly string[] PreferredStack =
        { ".net", "asp.net", "blazor", "iis", "sql server", "sqlserver", "azure" };
    private static readonly string[] StrongStack =
        { "api", "database", "deployment", "dns", "tls", "ssl" };
    private static readonly string[] MediumHighStack =
        { "wordpress", "woocommerce", "shopify" };
    private static readonly string[] MediumStack =
        { "python", "node", "node.js", "php", "laravel" };

    public static ScoreBreakdown Score(ScoringInput input, DateTimeOffset now)
    {
        var b = new ScoreBreakdown
        {
            UrgencyScore = Urgency(input, now),
            StackFitScore = StackFit(input),
            BusinessValueScore = BusinessValue(input),
            ReachabilityScore = Reachability(input),
            CompetitionScore = Competition(input),
            TrustScore = Trust(input),
        };

        b.Total =
            b.UrgencyScore * WUrgency +
            b.StackFitScore * WStack +
            b.BusinessValueScore * WBusiness +
            b.ReachabilityScore * WReach +
            b.CompetitionScore * WCompetition +
            b.TrustScore * WTrust;

        // Freshness matters beyond urgency: stale pain posts are long since resolved.
        // Dampen the whole score by age so fresh organic pain always outranks old
        // inventory. An UNCLAIMED post with real stated money (an open bounty, a named
        // budget) decays slower — the cash stays claimable until someone takes it, so it
        // gets a floor rather than the full decay (claimed work already caps at 35).
        // Manual entries are exempt — the operator chose to add them.
        if (input.SourceKey != "manual")
        {
            var ageDays = (now - input.PostedAt).TotalDays;
            var freshness = ageDays <= 7 ? 1.0 : ageDays <= 30 ? 0.9 : ageDays <= 90 ? 0.75 : 0.6;
            if (HasOpenStatedMoney(input)) freshness = Math.Max(freshness, 0.85);
            b.Total *= freshness;
        }

        // Worldwide work is still eligible, but a language barrier adds response and
        // clarification friction. Apply the penalty before thresholds/priority so only
        // otherwise-strong foreign-language leads survive into the review queue.
        if (IsNonEnglish(input.LanguageCode))
        {
            b.LanguagePenalty = NonEnglishPenalty;
            b.Total = Math.Max(0, b.Total - b.LanguagePenalty);
        }
        b.Total = Math.Round(b.Total, 1);

        // A lead with no willingness-to-pay signal from any layer (source, AI judgment,
        // or explicit hire/budget language) is an advice-seeker: keep it visible for
        // manual review, but cap it below the Medium band (55) so it can never outrank
        // a lead that carries an actual pay signal.
        if (!HasPaySignal(input))
            b.Total = Math.Min(b.Total, 52);

        // The operator is a pure-stack consultant: a lead that never matches a single
        // stack-identity skill (C#/.NET/SQL Server/Azure…) may be payable work, but it is
        // someone else's work — keep it visible for manual review, capped below Medium.
        if (input.SkillMatches is not null && !SkillMatcher.HasStackIdentityMatch(input.SkillMatches))
            b.Total = Math.Min(b.Total, 50);

        // Wrong stack is a hard gate: a post demanding a stack outside the profile
        // (Go/Python/Java job posts…) that never touches the operator's own stack is not
        // actionable no matter how strong its pay intent — the operator can't do the work.
        // "Touches the own stack" means a real identity match (C#, .NET, SQL Server…),
        // not a transferable capability phrase like "REST API" that every job post contains.
        if (input.ForeignStackDemands.Count > 0 &&
            !(input.SkillMatches is { } fm && SkillMatcher.HasStackIdentityMatch(fm)))
            b.Total = Math.Min(b.Total, 35);

        // Someone else already owns the work (assigned issue, PR being merged): the
        // opportunity is effectively missed — keep it around as a Watch-tier record at
        // most, no matter how strong every other signal is.
        if (input.ClaimedByOthers)
            b.Total = Math.Min(b.Total, 35);
        // A crowd of visible responders means the same thing in slow motion: with 8+
        // people already engaging (bounty hunters commenting, thread full of helpers),
        // the odds of being the one who gets paid are near zero.
        else if (input.CompetingResponses >= 8)
            b.Total = Math.Min(b.Total, 45);

        b.Priority = ToPriority(b.Total);
        return b;
    }

    public static bool IsNonEnglish(string? languageCode) =>
        !string.IsNullOrWhiteSpace(languageCode) &&
        !languageCode.Equals("en", StringComparison.OrdinalIgnoreCase) &&
        !languageCode.Equals("eng", StringComparison.OrdinalIgnoreCase);

    /// <summary>Count of explicit "pay:" hits the pre-filter tagged (hire language, budgets, money amounts).</summary>
    private static int PayHits(ScoringInput i) =>
        i.PreFilter?.MatchedTerms.Count(t => t.StartsWith("pay:", StringComparison.OrdinalIgnoreCase)) ?? 0;

    /// <summary>Any evidence the poster would actually pay: pay-intent source, AI judgment, or pay language.</summary>
    private static bool HasPaySignal(ScoringInput i) =>
        IsPayIntentSource(i.SourceKey) ||
        i.Ai?.PaymentIntent is "Explicit" or "Implied" ||
        PayHits(i) > 0;

    /// <summary>
    /// Unclaimed work with meaningful stated compensation ($100 mirrors the operator's
    /// minimum fee — micro-bounties don't earn the slower age decay).
    /// </summary>
    private static bool HasOpenStatedMoney(ScoringInput i) =>
        i.OfferedAmount is >= 100 && !i.ClaimedByOthers;

    public static Priority ToPriority(double total) => total switch
    {
        >= 85 => Priority.Critical,
        >= 70 => Priority.High,
        >= 55 => Priority.Medium,
        >= 40 => Priority.Watch,
        _ => Priority.Low
    };

    private static double Urgency(ScoringInput i, DateTimeOffset now)
    {
        double score = 30;
        if (i.Ai is { } ai)
        {
            if (ai.IsEmergency) score = 82;
            else if (ai.IsTechnicalProblem) score = 48;
            score += CategorySeverityBonus(ai.ProblemCategory);
            score = score * (0.7 + 0.3 * (double)ai.AiConfidence); // temper by confidence
        }
        else if (i.PreFilter is { } pf)
        {
            score = Math.Clamp((double)pf.HeuristicScore + 30, 0, 80);
        }

        // Recency decay: fresh posts are more actionable. Open stated-money work
        // (unclaimed bounty / named budget) keeps a floor — the offer doesn't expire
        // with the news cycle the way organic pain does.
        var ageHours = (now - i.PostedAt).TotalHours;
        double recency = ageHours <= 1 ? 1.0 : ageHours <= 6 ? 0.92 : ageHours <= 24 ? 0.8 : ageHours <= 72 ? 0.6 : 0.35;
        if (HasOpenStatedMoney(i)) recency = Math.Max(recency, 0.6);
        return Math.Clamp(score * recency, 0, 100);
    }

    private static double CategorySeverityBonus(string category) => category switch
    {
        "Data Loss" => 18,
        "Production Outage" => 15,
        "Database Failure" => 14,
        "Payment/Checkout Failure" => 14,
        "Website Down" => 12,
        "Deployment Failure" => 12,
        "API Failure" => 10,
        "DNS/TLS Failure" => 10,
        "Authentication/Login Failure" => 9,
        "Performance Emergency" => 8,
        "Security Incident" => 4, // relevant but authorization-risky
        "Modernization/Migration" => -6, // planned consulting work, not time-critical
        "Feature Request" => -10, // real paid work, but never time-critical
        "Non-Urgent Help Request" => -20,
        "Not Relevant" => -30,
        _ => 0
    };

    private static double StackFit(ScoringInput i)
    {
        // The operator's configurable skill profile wins over the built-in stack lists.
        if (i.SkillMatches is not null)
        {
            var fit = SkillMatcher.FitScore(i.SkillMatches);
            // Every distinct foreign-stack demand dilutes the fit; without a stack-identity
            // match the post is fundamentally someone else's work.
            fit -= Math.Min(i.ForeignStackDemands.Count, 3) * 15;
            if (i.ForeignStackDemands.Count > 0 && !SkillMatcher.HasStackIdentityMatch(i.SkillMatches))
                fit = Math.Min(fit, 25);
            return Math.Clamp(fit, 0, 100);
        }

        var stack = i.Ai?.DetectedStack ?? new List<string>();
        var joined = string.Join(' ', stack).ToLowerInvariant();
        if (PreferredStack.Any(joined.Contains)) return 95;
        if (StrongStack.Any(joined.Contains)) return 78;
        if (MediumHighStack.Any(joined.Contains)) return 65;
        if (MediumStack.Any(joined.Contains)) return 50;
        return stack.Count > 0 ? 45 : 35; // unknown stack -> depends on clarity
    }

    /// <summary>Sources where every item is a business explicitly offering to pay (job posts, hiring subs).</summary>
    private static readonly string[] PayIntentSources = { "remotive", "rss", "reddit", "rss_jobs", "reddit_hiring" };

    private static double BusinessValue(ScoringInput i)
    {
        double score = 40;
        if (i.Ai is { } ai)
        {
            score = ai.ProblemCategory switch
            {
                "Payment/Checkout Failure" => 90,
                "Production Outage" => 85,
                "Data Loss" => 85,
                "Database Failure" => 80,
                "Website Down" => 72,
                "Deployment Failure" => 70,
                "API Failure" => 68,
                "DNS/TLS Failure" => 66,
                "Authentication/Login Failure" => 64,
                "Performance Emergency" => 58,
                "Security Incident" => 45,
                "Modernization/Migration" => 75, // multi-week engagements: the highest-value non-incident work
                "Feature Request" => 50, // bounded paid implementation work
                "Non-Urgent Help Request" => 25,
                _ => 30
            };
            if (ai.EstimatedFixMinutesMax is > 0 and <= 240) score += 5; // bounded work is attractive
        }

        // Explicit commercial signals in the post (client/customers/orders/revenue…)
        // matter more than category guesses — count the pre-filter's "business:" hits.
        var commercialHits = i.PreFilter?.MatchedTerms
            .Count(t => t.StartsWith("business:", StringComparison.OrdinalIgnoreCase)) ?? 0;
        score += Math.Min(commercialHits, 4) * 4;

        // Willingness-to-pay is the whole point: explicit hire/budget/rate language
        // outranks every category guess, and an AI "None" verdict caps the value.
        score += Math.Min(PayHits(i), 3) * 8;
        score = i.Ai?.PaymentIntent switch
        {
            "Explicit" => Math.Max(score, 88),
            "Implied" => score + 10,
            "None" when !IsPayIntentSource(i.SourceKey) && PayHits(i) == 0 => Math.Min(score, 45),
            _ => score
        };

        // A job post / hiring thread is a person already committed to spending money.
        if (IsPayIntentSource(i.SourceKey)) score = Math.Max(score, 70);

        // A concrete stated amount overrides everything above: a $15 bounty is not
        // high-value work no matter how "Explicit" the intent is.
        score = i.OfferedAmount switch
        {
            < 25 => Math.Min(score, 40),
            < 100 => Math.Min(score, 62),
            >= 1000 => Math.Min(score + 8, 100),
            _ => score
        };

        return Math.Clamp(score, 0, 100);
    }

    private static double Reachability(ScoringInput i)
    {
        double baseScore = SourceReputation(i.SourceKey);
        if (i.HasContact) baseScore += 10;
        return Math.Clamp(baseScore, 0, 100);
    }

    private static double Competition(ScoringInput i)
    {
        // Higher score = less competition / more room to be the responder. Observed
        // engagement on the post itself (comments, thread replies) outweighs the
        // source-level prior: every visible responder is someone already ahead of us.
        var observedPenalty = Math.Min(i.CompetingResponses, 8) * 5;
        return Math.Clamp(SourceBaseCompetition(i) - observedPenalty, 5, 100);
    }

    private static double SourceBaseCompetition(ScoringInput i)
    {
        return i.SourceKey switch
        {
            "hackernews" => 55,
            "hn_hiring" => 50,   // who-is-hiring threads draw many replies; speed matters
            "bounties_opire" => 48,          // bounties are visible to every hunter
            "github_bounties" => 48,
            "github_feature_requests" => 65, // few devs mine these — low competition
            "reddit_hiring" => 60,
            "reddit_wordpress_shopify" => 55,
            "reddit_webdev_ops" => 52,
            "reddit_stacks" => 52,
            "reddit_saas_tools" => 56,   // fewer devs reading; non-technical posters
            "reddit_business_ecommerce" => 54,
            "reddit_pain" => 50,
            "reddit" => 60,      // hiring threads move fast but replies are low-effort
            "rss_jobs" => 45,
            "rss_support" => 55,
            "rss" => 45,         // public job boards attract many applicants
            "remotive" => 45,    // same — speed and specificity are the edge
            "stackexchange_radar" => 45,
            "stackexchange" => 45, // answers arrive fast
            "manual" => 80,      // operator-sourced, usually exclusive
            _ => 55
        };
    }

    private static double Trust(ScoringInput i)
    {
        if (i.RedFlagged) return 5;
        double score = SourceReputation(i.SourceKey);
        if (i.Ai is { } ai)
        {
            score = score * 0.6 + (double)ai.AiConfidence * 100 * 0.4;
            if (ai.ProblemCategory == "Security Incident") score -= 20; // authorization risk
        }
        return Math.Clamp(score, 0, 100);
    }

    private static double SourceReputation(string sourceKey) => sourceKey switch
    {
        "manual" => 85,       // operator vetted it personally
        "remotive" => 80,     // verified companies paying to post
        "rss_jobs" => 75,
        "rss_support" => 58,
        "rss" => 75,          // job boards (WeWorkRemotely) — companies paying to post
        "hackernews" => 70,   // founders/operators, real businesses
        "hn_hiring" => 75,    // hiring threads: posters are explicitly offering to pay
        "bounties_opire" => 78,          // money is already escrowed/attached
        "github_bounties" => 74,
        "github_feature_requests" => 60, // pay language present, but not committed money
        "reddit_hiring" => 62,
        "reddit_wordpress_shopify" => 58,
        "reddit_webdev_ops" => 55,
        "reddit_stacks" => 54,
        "reddit_saas_tools" => 58,    // business users of paid tools — plausible budgets
        "reddit_business_ecommerce" => 56,
        "reddit_pain" => 52,
        "reddit" => 62,       // hiring subs are payable but identity is thin
        "stackexchange_radar" => 50,
        "stackexchange" => 50, // real problems, but askers usually want free answers
        _ => 55
    };

    private static bool IsPayIntentSource(string sourceKey)
    {
        if (PayIntentSources.Contains(sourceKey)) return true;
        return sourceKey.Contains("jobs", StringComparison.OrdinalIgnoreCase)
               || sourceKey.Contains("hiring", StringComparison.OrdinalIgnoreCase)
               || sourceKey.Contains("contract", StringComparison.OrdinalIgnoreCase)
               || sourceKey.Contains("bount", StringComparison.OrdinalIgnoreCase) // bounty = money attached
               || sourceKey.Contains("opire", StringComparison.OrdinalIgnoreCase);
    }
}
