using System.Text.RegularExpressions;

namespace DevLeads.Core;

/// <summary>Shared lead-quality rules used before a post reaches the review queue.</summary>
public static class LeadQualityRules
{
    /// <summary>
    /// Default freshness window for automated discovery. Older public posts are normally
    /// no longer actionable; manual and operator-engaged leads are preserved separately.
    /// </summary>
    public const int MaxAutomatedLeadAgeDays = 30;

    public static bool IsWithinAutomatedLeadAge(DateTimeOffset postedAt, DateTimeOffset now) =>
        postedAt >= now.AddDays(-MaxAutomatedLeadAgeDays);

    private static readonly Regex EmailPattern = new(
        @"[a-z0-9._%+-]+@[a-z0-9.-]+\.[a-z]{2,}",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex UrlHostPattern = new(
        @"https?://(?<host>[a-z0-9.-]+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex NonWordPattern = new(@"[^a-z0-9]+", RegexOptions.Compiled);

    // First-person hire/pay language only. Bare topic nouns ("freelancer", "budget",
    // "flat fee", "consultant") match product pricing copy and career discussion posts,
    // which is how ads and announcements used to reach the top of the lead list.
    private static readonly string[] ThirdPartyPaySignals =
    {
        "[hiring]", "[task]", "[paid]", "willing to pay", "will pay", "can pay",
        "happy to pay", "ready to pay", "paid help", "paid gig", "paid task",
        "paid support", "paid work", "hourly rate", "your rate",
        "fixed price", "for a flat fee", "retainer", "compensation", "compensate",
        "bounty", "looking to hire", "need to hire", "hire someone", "for a fee",
        "how much would it cost", "how much to fix", "cost to fix", "price to fix",
        "we pay", "pay you to", "will pay you", "name your price", "send me a quote",
        "budget of", "budget is", "have a budget", "budget for this", "budget around",
        "looking for a freelancer", "need a freelancer", "hire a freelancer", "freelancer needed",
        "freelance gig", "freelance project",
        "hire a contractor", "looking for a contractor", "contractor needed",
        "hire a consultant", "looking for a consultant", "need a consultant", "consultant needed"
    };

    private static readonly string[] AntiPaySignals =
    {
        "no budget", "without a budget", "can't afford", "cannot afford",
        "for free", "free of charge", "without paying", "unpaid",
        "won't pay anyone", "don't want to pay", "do not want to pay"
    };

    private static readonly string[] VendorSupportSignals =
    {
        "account recovery", "account suspended", "account locked", "account banned",
        "account disabled", "account flagged", "account under review", "restore my account",
        "reactivate my account", "unban", "unblock my account", "locked out of my account",
        "account email", "updated payment method", "updated payment information",
        "manual charge", "retry the payment", "process the payment", "payment failed",
        "failed payment", "billing issue", "billing problem", "charged twice",
        "double charged", "refund", "payment could not be completed",
        "payment information with a new", "restore my landing pages",
        "reactivate the following landing pages", "all of my landing pages were suspended",
        "contact support", "contacted support", "support team", "support ticket",
        "opened a ticket", "open a ticket", "ticket number", "case number",
        "my ticket", "escalate this", "on your end", "on your side",
        "can the team", "anyone from the team", "staff help", "quota increase",
        "rate limit increase", "increase my limit", "verification pending",
        "pending verification", "waiting for approval", "approval pending",
        "payout on hold", "payouts on hold", "funds on hold",
        "expected behavior", "actual behavior", "reproduction steps",
        "i have reproduced the issue", "latest cli version",
        "this report is not a duplicate", "isn't a duplicate",
        "bug report", "steps to reproduce",
        "can you reset", "please reset", "can you enable", "please enable",
        "can you unlock", "please unlock", "can you restore", "please restore",
        "can you reactivate", "please reactivate", "can you verify",
        "please verify my", "can you check my account", "can you check our account",
        "please update my account", "can you look into my", "please look into my",
        "please lift the"
    };

    private static readonly string[] ResolvedSignals =
    {
        "acceptedanswer", "accepted_answer", "itemprop=\"acceptedanswer\"",
        "\"accepted_answer_post_id\"", "\"has_accepted_answer\":true",
        "problem solved", "issue solved", "this is solved", "it is solved",
        "resolved now", "this is resolved", "fixed now", "got it working",
        "works now", "no longer need help", "no longer needs help",
        "thanks, that fixed it", "thank you, that fixed it",
        "marked as solved", "solution found",
        // Discourse/WordPress feeds mark resolved topics with an inline HTML badge.
        "topic is resolved", "class=\"resolved\"", "aria-label=\"resolved\""
    };

    private static readonly string[] ConcretePaidSources =
    {
        "remotive", "rss_jobs", "hn_hiring", "reddit_hiring",
        "bounties_opire", "github_bounties", "github_feature_requests"
    };

    /// <summary>Launch/showcase language typical of product announcements, not help requests.</summary>
    private static readonly string[] LaunchSignals =
    {
        "we built", "we've built", "i built", "we created", "we launched",
        "we just launched", "just launched", "introducing ", "excited to announce",
        "excited to share", "check out our", "early access", "join the waitlist",
        "founding member", "founding community", "beta testers"
    };

    /// <summary>Pricing/marketing copy: the money language belongs to the product, not a hire offer.</summary>
    private static readonly string[] PricingCopySignals =
    {
        "/month", "/mo", "per month", "free plan", "free tier", "free trial",
        "free up to", "no credit card", "sign up at", "get started at",
        "pricing starts", "lifetime deal"
    };

    /// <summary>Words that mark an actual problem report — their presence vetoes the promo verdict.</summary>
    private static readonly string[] ProblemReportSignals =
    {
        "error", "broken", "not working", "failing", "failed", "is down",
        "went down", "bug", "help me", "stuck", "crash", "urgent"
    };

    /// <summary>
    /// True for product-launch/showcase posts: launch language plus the poster's own pricing
    /// copy, with no actual problem being reported. Their "flat fee"/"$X/month" wording is
    /// marketing, and must never read as willingness to pay a third party.
    /// </summary>
    public static bool IsPromotionalAnnouncement(string text) =>
        LaunchSignals.Any(s => text.Contains(s, StringComparison.OrdinalIgnoreCase)) &&
        PricingCopySignals.Any(s => text.Contains(s, StringComparison.OrdinalIgnoreCase)) &&
        !ProblemReportSignals.Any(s => text.Contains(s, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// True for feed items that are replies into an existing thread (WordPress.org reply
    /// feeds emit "Reply To: …" items) — the reply author is answering, not asking.
    /// </summary>
    public static bool IsReplyFeedItem(string title) =>
        title.TrimStart().StartsWith("Reply To:", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// True for GitHub issues published as tasks for autonomous AI agents ("agent-task"
    /// label, "[AGENT-TASK]" titles). They pose as bounties but are agent-economy bait,
    /// not work a human consultant gets hired and paid for.
    /// </summary>
    /// <summary>"[meta] assignees:N comments:M" line emitted by the GitHub connector.</summary>
    private static readonly Regex GitHubMetaPattern = new(
        @"\[meta\]\s+assignees:(?<assignees>\d+)\s+comments:(?<comments>\d+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>Discourse RSS footer: "3 posts - 2 participants".</summary>
    private static readonly Regex DiscourseFooterPattern = new(
        @"(?<posts>\d+)\s+posts?\s+-\s+(?<participants>\d+)\s+participants?",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>Someone has already claimed the work or has a fix in flight.</summary>
    private static readonly string[] ClaimedWorkSignals =
    {
        "i'm working on this", "im working on this", "i am working on this",
        "working on this issue", "already working on", "i'll take this", "i can take this",
        "opened a pr", "pr is up", "pr is open", "submitted a pr", "submitted a pull request",
        "created a pull request", "opened a pull request", "linked a pull request",
        "ready to merge", "being merged", "merging my changes", "/attempt"
    };

    /// <summary>
    /// True when the post shows someone else already owns the work: the issue is assigned,
    /// or the visible text contains claim/PR-in-flight language. Such opportunities are
    /// effectively missed regardless of how well they score otherwise.
    /// </summary>
    public static bool IsAlreadyClaimed(string text)
    {
        var meta = GitHubMetaPattern.Match(text);
        if (meta.Success && int.Parse(meta.Groups["assignees"].Value) > 0) return true;
        return ClaimedWorkSignals.Any(s => text.Contains(s, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// How many other people are already engaging with the post: GitHub comments, or
    /// Discourse thread participants beyond the author. 0 when unknown.
    /// </summary>
    public static int CompetingResponseCount(string text)
    {
        var meta = GitHubMetaPattern.Match(text);
        if (meta.Success) return int.Parse(meta.Groups["comments"].Value);

        var discourse = DiscourseFooterPattern.Match(text);
        if (discourse.Success) return Math.Max(0, int.Parse(discourse.Groups["participants"].Value) - 1);

        return 0;
    }

    public static bool IsAiAgentTaskPost(string title, string text) =>
        title.Contains("[AGENT-TASK]", StringComparison.OrdinalIgnoreCase) ||
        text.Contains("agent-task", StringComparison.OrdinalIgnoreCase) ||
        text.Contains("for ai agents", StringComparison.OrdinalIgnoreCase) ||
        text.Contains("autonomous agents only", StringComparison.OrdinalIgnoreCase);

    public static bool HasThirdPartyPayOffer(string text) =>
        !AntiPaySignals.Any(s => text.Contains(s, StringComparison.OrdinalIgnoreCase)) &&
        ThirdPartyPaySignals.Any(s => text.Contains(s, StringComparison.OrdinalIgnoreCase));

    public static bool IsVendorControlledSupportRequest(string text) =>
        VendorSupportSignals.Any(s => text.Contains(s, StringComparison.OrdinalIgnoreCase));

    public static bool IsNonHirableVendorSupportRequest(string text) =>
        IsVendorControlledSupportRequest(text) && !HasThirdPartyPayOffer(text);

    public static bool IsResolvedOrClosedRequest(string text) =>
        ResolvedSignals.Any(s => text.Contains(s, StringComparison.OrdinalIgnoreCase));

    public static bool IsConcretePaidSource(string sourceKey)
    {
        if (ConcretePaidSources.Contains(sourceKey)) return true;
        return sourceKey.Contains("jobs", StringComparison.OrdinalIgnoreCase)
               || sourceKey.Contains("hiring", StringComparison.OrdinalIgnoreCase)
               || sourceKey.Contains("bount", StringComparison.OrdinalIgnoreCase)
               || sourceKey.Contains("opire", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsDashboardWorthyLead(
        string sourceKey,
        string paymentIntent,
        bool? assistanceRequested,
        bool feeIsEstimate,
        string text)
    {
        if (IsNonHirableVendorSupportRequest(text) || IsResolvedOrClosedRequest(text))
            return false;

        if (assistanceRequested == false)
            return false;

        // A business owner/operator with a concrete hands-on request is worth a human
        // look even before payment is discussed. AI/heuristic triage uses "Implied" only
        // for first-person ownership, making it the networking tier below paid work.
        if (paymentIntent == "Implied")
            return assistanceRequested == true;

        // The command center should surface concrete paid situations first. Support
        // forum urgency is too often free troubleshooting, solved, or vendor-controlled.
        if (IsConcretePaidSource(sourceKey))
            return paymentIntent == "Explicit" || HasThirdPartyPayOffer(text);

        return paymentIntent == "Explicit" && !feeIsEstimate && HasThirdPartyPayOffer(text);
    }

    public static string NormalizeDuplicateTitle(string title)
    {
        var normalized = NonWordPattern.Replace(title.Trim().ToLowerInvariant(), " ").Trim();
        return string.Join(' ', normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    public static string? HostFromUrl(string? url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return null;
        return NormalizeHost(uri.Host);
    }

    public static bool SharesDuplicateClue(string leftText, string rightText)
    {
        var left = ExtractDuplicateClues(leftText);
        if (left.Count == 0) return false;
        return ExtractDuplicateClues(rightText).Any(left.Contains);
    }

    private static HashSet<string> ExtractDuplicateClues(string text)
    {
        var clues = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match m in EmailPattern.Matches(text))
            clues.Add("email:" + m.Value.ToLowerInvariant());

        foreach (Match m in UrlHostPattern.Matches(text))
        {
            var host = NormalizeHost(m.Groups["host"].Value);
            if (!string.IsNullOrWhiteSpace(host))
                clues.Add("host:" + host);
        }

        return clues;
    }

    private static string NormalizeHost(string host) =>
        host.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? host[4..].ToLowerInvariant() : host.ToLowerInvariant();
}
