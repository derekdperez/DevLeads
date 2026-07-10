using System.Text.RegularExpressions;
using DevLeads.Core.Entities;
using DevLeads.Core.QueryPacks;

namespace DevLeads.Core;

/// <summary>
/// Zero-cost keyword/rule filter deciding whether a raw item is worth an LLM call.
/// Protects the AI budget, cuts latency, and rejects obvious noise before triage.
/// </summary>
public sealed class HeuristicPreFilter
{
    private readonly IQueryPackProvider _queryPacks;

    public HeuristicPreFilter(IQueryPackProvider queryPacks) => _queryPacks = queryPacks;

    private static readonly string[] UrgencySignals =
    {
        "urgent", "asap", "right now", "today", "this morning", "down", "outage",
        "broken", "failing", "failed", "stopped working", "critical", "emergency",
        "production", "live site", "customers cannot", "customers can't", "client",
        "orders", "payments", "revenue"
    };

    private static readonly string[] TechnicalSignals =
    {
        ".net", "asp.net", "blazor", "iis", "azure", "sql", "sql server", "database",
        "api", "webhook", "stripe", "checkout", "woocommerce", "shopify", "wordpress",
        "server", "hosting", "dns", "ssl", "tls", "certificate", "login", "auth",
        "oauth", "deployment", "deploy", "500", "502", "503", "timeout", "cloudflare",
        "nginx", "apache", "web.config", "connection string", "developer", "engineer",
        "full-stack", "full stack", "backend", "frontend", "devops", "integration",
        "github", "repository", "open source", "feature request", "plugin", "sdk"
    };

    /// <summary>
    /// First-person business-ownership signals. Compound phrases on purpose: bare nouns
    /// ("payments", "customers", "checkout") match every post that merely discusses
    /// payment/e-commerce technology and say nothing about the poster's stake in it.
    /// </summary>
    private static readonly string[] CommercialSignals =
    {
        "our customers", "my customers", "our clients", "my client", "our users",
        "our store", "my store", "our shop", "my shop", "our business", "my business",
        "our company", "my company", "our saas", "my saas", "our site", "my site",
        "our website", "my website", "our production", "our revenue", "my revenue",
        "losing sales", "losing money", "losing revenue", "lost orders", "losing customers",
        "we are losing", "we're losing", "costing us", "costing me", "our agency", "my agency"
    };

    /// <summary>
    /// Explicit willingness-to-pay language. These are the strongest signal we have that a
    /// post is a lead and not an advice request, so they get their own group, their own
    /// weight, and a "pay:" tag the scorer keys off.
    /// </summary>
    private static readonly string[] PayIntentSignals =
    {
        "[hiring]", "[task]", "[paid]", "willing to pay", "will pay", "can pay", "happy to pay",
        "ready to pay", "paying for", "paid help", "paid gig", "paid task", "paid support",
        "paid work", "hourly rate", "your rate", "your rates", "fixed price",
        "for a flat fee", "retainer", "compensation", "compensate", "bounty",
        "looking to hire", "need to hire", "hire someone", "hiring", "for a fee",
        "how much would it cost", "how much to fix", "cost to fix", "price to fix",
        "we pay", "pay you to", "will pay you", "name your price", "send me a quote",
        // "budget"/"freelancer"/"consultant" as bare nouns are topic keywords ("wasting my
        // ad budget", career threads, product pricing) — only hire-shaped phrases count.
        "budget of", "budget is", "have a budget", "budget for this", "budget around",
        "looking for a freelancer", "need a freelancer", "hire a freelancer", "freelancer needed",
        "freelance gig", "freelance project",
        "hire a contractor", "looking for a contractor", "contractor needed",
        "hire a consultant", "looking for a consultant", "need a consultant", "consultant needed"
    };

    /// <summary>Concrete money amounts ("$500", "40/hr", "1500 USD") — strong pay evidence.</summary>
    private static readonly Regex MoneyPattern = new(
        @"[\$€£]\s?\d|\b\d[\d,.]*\s?(usd|eur|gbp|dollars)\b|\b\d+\s?/\s?(hr|hour)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Asker-side negations that invalidate pay signals ("no budget", "for free").
    /// Deliberately excludes "customers cannot pay"-style phrases — those are business
    /// impact, not a refusal to hire.
    /// </summary>
    private static readonly string[] AntiPaySignals =
    {
        "no budget", "without a budget", "can't afford", "cannot afford",
        "for free", "free of charge", "without paying", "unpaid",
        "won't pay anyone", "don't want to pay", "do not want to pay"
    };

    /// <summary>True when the text contains explicit hire/pay language or a money amount, un-negated.</summary>
    public static bool HasPayLanguage(string text) =>
        !AntiPaySignals.Any(s => text.Contains(s, StringComparison.OrdinalIgnoreCase)) &&
        (PayIntentSignals.Any(s => text.Contains(s, StringComparison.OrdinalIgnoreCase)) ||
         LeadQualityRules.HasThirdPartyPayOffer(text) ||
         MoneyPattern.IsMatch(text));

    public PreFilterResult Analyze(RawSourceItem item)
    {
        var text = $"{item.Title}\n{item.BodyText}".ToLowerInvariant();

        var highPriorityTerms = _queryPacks.GetHighPriorityTerms();
        var negativeTerms = _queryPacks.GetNegativeTerms();

        var matchedHighPriority = highPriorityTerms
            .Where(term => text.Contains(term.ToLowerInvariant()))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var matchedNegative = negativeTerms
            .Where(term => text.Contains(term.ToLowerInvariant()))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var urgencyHits = MatchSignals(text, UrgencySignals);
        var technicalHits = MatchSignals(text, TechnicalSignals);
        var commercialHits = MatchSignals(text, CommercialSignals);
        var payHits = MatchSignals(text, PayIntentSignals);
        if (MoneyPattern.IsMatch(text)) payHits.Add("$amount");
        // "no budget" / "can't pay" invalidates every pay signal in the post.
        if (AntiPaySignals.Any(s => text.Contains(s, StringComparison.OrdinalIgnoreCase)))
            payHits.Clear();
        var inferredMatches = urgencyHits.Select(s => $"urgency:{s}")
            .Concat(technicalHits.Select(s => $"tech:{s}"))
            .Concat(commercialHits.Select(s => $"business:{s}"))
            .Concat(payHits.Select(s => $"pay:{s}"))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        string? hardRejectReason =
            LeadQualityRules.IsReplyFeedItem(item.Title)
                ? "Feed item is a reply into an existing thread, not a new request."
            : LeadQualityRules.IsAiAgentTaskPost(item.Title, text)
                ? "Task published for autonomous AI agents, not hirable human work."
            : LeadQualityRules.IsPromotionalAnnouncement(text)
                ? "Promotional product/launch announcement, not a request for help."
            : LeadQualityRules.IsResolvedOrClosedRequest(text)
                ? "Thread is already solved or has an accepted answer."
            : null;

        if (hardRejectReason is not null)
        {
            return new PreFilterResult
            {
                ShouldAnalyzeWithAi = false,
                KeywordHitCount = matchedHighPriority.Count + matchedNegative.Count + inferredMatches.Count,
                HighPriorityHitCount = matchedHighPriority.Count,
                NegativeHitCount = matchedNegative.Count,
                PayIntentHitCount = 0,
                HeuristicScore = 0,
                MatchedTerms = matchedHighPriority.Concat(matchedNegative).Concat(inferredMatches).ToList(),
                RejectReason = hardRejectReason
            };
        }

        if (LeadQualityRules.IsNonHirableVendorSupportRequest(text))
        {
            return new PreFilterResult
            {
                ShouldAnalyzeWithAi = false,
                KeywordHitCount = matchedHighPriority.Count + matchedNegative.Count + inferredMatches.Count,
                HighPriorityHitCount = matchedHighPriority.Count,
                NegativeHitCount = matchedNegative.Count,
                PayIntentHitCount = 0,
                HeuristicScore = 0,
                MatchedTerms = matchedHighPriority.Concat(matchedNegative).Concat(inferredMatches).ToList(),
                RejectReason = "Vendor account/billing support request, not hirable third-party repair work."
            };
        }

        var heuristicScore =
            matchedHighPriority.Count * 20 +
            Math.Min(payHits.Count, 3) * 12 +
            Math.Min(urgencyHits.Count, 4) * 6 +
            Math.Min(technicalHits.Count, 5) * 5 +
            Math.Min(commercialHits.Count, 3) * 4 -
            matchedNegative.Count * 18;

        var hasInferredEmergency = urgencyHits.Count > 0 && technicalHits.Count > 0;
        var hasCommercialTechnicalSignal = commercialHits.Count > 0 && technicalHits.Count > 1;
        // Someone naming a budget/rate next to a technical topic is a lead even without
        // emergency wording — never reject those before triage.
        var hasHireSignal = payHits.Count > 0 && technicalHits.Count > 0;

        // Rule 1: no emergency/technical/hire trigger at all -> reject.
        if (matchedHighPriority.Count == 0 && !hasInferredEmergency && !hasCommercialTechnicalSignal && !hasHireSignal)
        {
            return new PreFilterResult
            {
                ShouldAnalyzeWithAi = false,
                KeywordHitCount = matchedNegative.Count + inferredMatches.Count,
                HighPriorityHitCount = 0,
                NegativeHitCount = matchedNegative.Count,
                PayIntentHitCount = payHits.Count,
                HeuristicScore = heuristicScore,
                MatchedTerms = matchedNegative.Concat(inferredMatches).ToList(),
                RejectReason = "No emergency or technical trigger terms matched."
            };
        }

        // Rule 2: dominated by exclusion language and only a weak trigger -> reject.
        if (matchedNegative.Count >= 2 && matchedHighPriority.Count < 2 && urgencyHits.Count < 2)
        {
            return new PreFilterResult
            {
                ShouldAnalyzeWithAi = false,
                KeywordHitCount = matchedHighPriority.Count + matchedNegative.Count,
                HighPriorityHitCount = matchedHighPriority.Count,
                NegativeHitCount = matchedNegative.Count,
                PayIntentHitCount = payHits.Count,
                HeuristicScore = heuristicScore,
                MatchedTerms = matchedHighPriority.Concat(matchedNegative).Concat(inferredMatches).ToList(),
                RejectReason = "Likely educational, unpaid, or low-commercial-value post."
            };
        }

        // Survivor: worth an AI call.
        return new PreFilterResult
        {
            ShouldAnalyzeWithAi = true,
            KeywordHitCount = matchedHighPriority.Count + matchedNegative.Count + inferredMatches.Count,
            HighPriorityHitCount = matchedHighPriority.Count,
            NegativeHitCount = matchedNegative.Count,
            PayIntentHitCount = payHits.Count,
            HeuristicScore = heuristicScore,
            MatchedTerms = matchedHighPriority.Concat(matchedNegative).Concat(inferredMatches).ToList()
        };
    }

    private static List<string> MatchSignals(string text, IEnumerable<string> signals) =>
        signals.Where(signal => text.Contains(signal, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
}
