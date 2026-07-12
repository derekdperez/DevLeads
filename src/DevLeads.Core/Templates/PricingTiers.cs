namespace DevLeads.Core.Templates;

public sealed record PricingTier(string Name, string UseCase, double SuggestedMin, double SuggestedMax);

/// <summary>Suggested pricing tiers used by the quote generator and detail UI.</summary>
public static class PricingTiers
{
    public static readonly IReadOnlyList<PricingTier> All = new List<PricingTier>
    {
        new("Quick diagnostic", "Confirm cause / fixability", 100, 250),
        new("Small emergency fix", "Bounded issue, payment on completion", 250, 750),
        new("Production repair", "Site / API / database down", 750, 1500),
        new("Critical rescue", "Multi-system outage", 1500, 3500),
        new("Modernization engagement", "Legacy migration / replatform project", 2000, 10000),
    };

    /// <summary>Chooses a tier from a category, returning a (min,max) suggested fee.</summary>
    public static (double Min, double Max) SuggestFor(string problemCategory) => problemCategory switch
    {
        "Data Loss" or "Production Outage" => (1500, 3500),
        "Database Failure" or "Website Down" or "API Failure" or "Deployment Failure" or "DNS/TLS Failure"
            => (750, 1500),
        "Payment/Checkout Failure" or "Authentication/Login Failure" or "Performance Emergency"
            => (250, 750),
        "Feature Request" => (250, 1500), // scoped implementation work, priced by size
        "Modernization/Migration" => (2000, 10000), // multi-week consulting engagement
        "Non-Urgent Help Request" => (100, 250),
        _ => (250, 750)
    };
}
