using System.Text.Json;
using DevLeads.Core;
using DevLeads.Core.Ai;
using DevLeads.Core.Entities;

namespace DevLeads.Infrastructure.Ai;

/// <summary>
/// A zero-cost, no-network triage provider. Infers a plausible structured result from
/// keywords so the full pipeline runs end-to-end without an API key.
/// </summary>
public sealed class HeuristicTriageProvider : IAiTriageProvider
{
    public string Name => "Heuristic";

    public bool IsAvailable(OperatorSettings settings) => true;
    public string AvailabilityMessage(OperatorSettings settings) => "Always available (offline rules).";

    // Category keywords only — the stack is always detected from the text itself,
    // never asserted from a category guess.
    private static readonly (string[] Keywords, string Category)[] CategoryMap =
    {
        (new[]{"stripe","checkout","payment","woocommerce","shopify","cart","orders not","customers cannot pay","lost orders"}, "Payment/Checkout Failure"),
        (new[]{"sql server","database down","connection string","deadlock","ef core migration","database unavailable","database connection failed","database locked"}, "Database Failure"),
        (new[]{"deploy","deployment","app pool","web.config","azure app service","iis 500","blazor deployment","kestrel failed","application pool stopped"}, "Deployment Failure"),
        (new[]{"dns","ssl","tls","certificate expired","not resolving","cloudflare 525","mx records"}, "DNS/TLS Failure"),
        (new[]{"oauth","login","authentication","can't log in","cannot log in","users cannot login","sso","token validation"}, "Authentication/Login Failure"),
        (new[]{"api down","api stopped","rest api","webhook","endpoint","api returning 500","api requests failing"}, "API Failure"),
        (new[]{"502","503","website down","site down","hosting down","wordpress site down","white screen","critical error on this website"}, "Website Down"),
        (new[]{"production down","production issue","outage","500 error production","production incident","live site is down"}, "Production Outage"),
        (new[]{"slow","timeout","performance","high cpu","memory leak","queue backed up","background jobs stuck"}, "Performance Emergency"),
        (new[]{"data loss","deleted data","dropped table","corrupt","lost data"}, "Data Loss"),
        (new[]{"breach","compromised","malware","ransomware","security incident"}, "Security Incident"),
        // Planned consulting work, not incidents — checked before Feature Request so
        // "migrate"/"modernize" language wins over generic enhancement wording.
        (new[]{"modernization","modernize","replatform","re-platform","migrate to .net","migrate from .net",
               ".net framework migration","legacy .net","legacy application","legacy codebase","legacy system",
               "webforms","web forms","wcf","winforms","vb6","vb.net","classic asp","silverlight",
               "cloud migration","azure migration","sql server upgrade","framework upgrade","rewrite legacy"}, "Modernization/Migration"),
        // Last: only when nothing above matched — scoped implementation work, not an incident.
        (new[]{"feature request","add support for","implement support","would be great if","enhancement",
               "bounty","sponsor this feature","fund this feature","paid feature"}, "Feature Request"),
    };

    // A post with none of these is not about software systems at all (e.g. a news article
    // about car manufacturing "production down") and must be classified Not Relevant.
    private static readonly string[] SoftwareContextSignals =
    {
        "website", "web site", "site ", "webapp", "web app", "app ", "application",
        "api", "server", "database", "sql", "software", "code", "developer", "dev ",
        "plugin", "theme", "wordpress", "woocommerce", "shopify", "stripe", "checkout",
        "login", "hosting", "domain", "dns", "ssl", "tls", "email", "smtp", "webhook",
        "deploy", "backend", "frontend", "bug", "error", "exception", "stack trace",
        ".net", "asp.net", "blazor", "iis", "azure", "aws", "node", "php", "python",
        "javascript", "react", "laravel", "django", "rails", "docker", "kubernetes",
        "saas", "crm", "e-commerce", "ecommerce", "full stack", "full-stack",
        "integration", "migration", "devops", "sre", "sysadmin",
        "github", "repository", "open source", "pull request", "issue tracker"
    };

    /// <summary>Job boards and hiring threads: the poster is already committed to paying.</summary>
    private static bool IsPayIntent(AiTriageRequest request)
    {
        var sourceKey = request.SourceKey.ToLowerInvariant();
        return sourceKey is "remotive" or "rss" or "reddit"
               || sourceKey.Contains("jobs", StringComparison.OrdinalIgnoreCase)
               || sourceKey.Contains("hiring", StringComparison.OrdinalIgnoreCase)
               || sourceKey.Contains("contract", StringComparison.OrdinalIgnoreCase)
               || sourceKey.Contains("bount", StringComparison.OrdinalIgnoreCase) // bounty = money attached
               || sourceKey.Contains("opire", StringComparison.OrdinalIgnoreCase)
               || sourceKey == "rss_jobs"
               || (sourceKey.StartsWith("reddit", StringComparison.OrdinalIgnoreCase)
                   && request.Title.Contains("hiring", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>The poster wants hands-on help, not just an answer.</summary>
    private static readonly string[] AssistanceSignals =
    {
        "help me", "can someone help", "can anyone help", "need someone", "looking for someone",
        "need a developer", "need a dev", "anyone available", "please help", "need help with",
        "someone to fix", "someone who can", "help us", "urgent help", "need assistance",
        "any help appreciated", "save me", "desperate"
    };

    /// <summary>
    /// First-person ownership of the broken business — the poster is the one losing money,
    /// so they're the one who'd pay. Deliberately compound phrases: bare topic keywords
    /// ("payments", "customers", "orders") tag every post that merely DISCUSSES payment or
    /// e-commerce technology, which says nothing about who's paying whom.
    /// </summary>
    private static readonly string[] ImpliedPaySignals =
    {
        "our customers", "my customers", "our clients", "my client", "our users",
        "our store", "my store", "our shop", "my shop", "our business", "my business",
        "our company", "my company", "our saas", "my saas", "our platform",
        "our production", "my production site", "our revenue", "my revenue",
        "losing sales", "losing money", "losing revenue", "losing orders", "losing customers",
        "we are losing", "we're losing", "costing us", "costing me", "my livelihood"
    };

    public Task<AiTriageResponse> TriageAsync(AiTriageRequest request, OperatorSettings settings, CancellationToken ct)
    {
        var text = $"{request.Title}\n{request.Body}".ToLowerInvariant();
        var redFlag = RedFlagDetector.Scan(request.Title, request.Body);
        // Source-based pay intent (job boards / hiring threads) drives the job-posting
        // branch below; explicit pay language in a problem post only upgrades the verdict.
        var explicitPayLanguage = HeuristicPreFilter.HasPayLanguage(text);
        var payIntent = IsPayIntent(request);

        // Gate: without any software context this is not our kind of problem, no matter
        // which emergency phrases matched (e.g. news about factory "production down").
        var hasSoftwareContext = SoftwareContextSignals.Any(s => text.Contains(s, StringComparison.OrdinalIgnoreCase));

        var category = hasSoftwareContext ? Classify(text) : "Not Relevant";
        bool isTechnical = hasSoftwareContext && category != "Not Relevant";

        // Job postings routinely contain words like "payments", "production", "client" in
        // their requirements — those are not incident signals. For pay-intent sources only
        // explicit urgency language counts; for problem posts the broader set applies.
        bool isEmergency;
        if (payIntent)
        {
            string[] urgentWords = { "urgent", "asap", "emergency", "immediately", "right away",
                "need fixed today", "site down", "site is down", "is broken", "not working" };
            isEmergency = isTechnical && urgentWords.Any(w => text.Contains(w, StringComparison.OrdinalIgnoreCase));
            if (!isEmergency && category is not ("Feature Request" or "Modernization/Migration"))
                category = "Non-Urgent Help Request"; // honest label: ongoing paid work, not an incident
        }
        else
        {
            // Compound phrases only: bare "down" matches "download"/"slowing down the page",
            // bare "client"/"orders"/"production" match every job post and product ad, and a
            // 2-term keyword coincidence is not an emergency.
            string[] emergencyPhrases =
            {
                "urgent", "asap", "emergency", "is down", "are down", "site down",
                "server down", "went down", "keeps going down", "stopped working",
                "not working", "broken", "failing", "customers cannot", "customers can't",
                "losing sales", "losing revenue", "losing money", "production is",
                "live site", "data loss", "lost data"
            };
            isEmergency = isTechnical && category is not "Non-Urgent Help Request" &&
                          emergencyPhrases.Any(p => text.Contains(p, StringComparison.OrdinalIgnoreCase));
        }

        if (text.Contains("homework") || text.Contains("class project") || text.Contains("learning") || text.Contains("leetcode"))
        {
            category = "Non-Urgent Help Request";
            isEmergency = false;
        }

        // Willingness-to-pay verdict, mirroring the AI prompt's rubric.
        var paymentIntent = explicitPayLanguage || payIntent ? "Explicit"
            : hasSoftwareContext && ImpliedPaySignals.Any(s => text.Contains(s, StringComparison.OrdinalIgnoreCase)) ? "Implied"
            : "None";

        // Hands-on help vs. information-only: job posts, bounties, and pay offers always
        // want a person; otherwise look for "help me / need someone" language.
        var assistanceRequested = payIntent || explicitPayLanguage ||
            AssistanceSignals.Any(s => text.Contains(s, StringComparison.OrdinalIgnoreCase));

        // Vendor-directed support request. Ownership language does NOT protect these:
        // "my account is suspended, can you reactivate it" is Implied-shaped but only the
        // provider's employees can act on it — no engagement possible. Only an explicit
        // offer to pay a third party overrides the signal.
        var vendorSupport = !explicitPayLanguage && !payIntent &&
            LeadQualityRules.IsVendorControlledSupportRequest(text);

        string recommendation;
        if (redFlag.IsRedFlagged) recommendation = "Do Not Contact";
        else if (!hasSoftwareContext) recommendation = "Ignore";
        else if (vendorSupport) recommendation = "Ignore"; // only the provider's team can fix it
        // Emergency-shaped but nobody with a stake asking for help (e.g. a developer
        // discussing a payment-library bug): watch it, don't queue it for review.
        else if (isEmergency && paymentIntent == "None" && !assistanceRequested) recommendation = "Watch";
        else if (isEmergency) recommendation = request.HeuristicScore >= 40 ? "Draft Reply" : "Manual Review";
        else if (payIntent) recommendation = "Manual Review"; // paid work, just not an outage
        else if (explicitPayLanguage && isTechnical) recommendation = "Manual Review"; // offering to pay — always worth a look
        else if (!isTechnical || category == "Non-Urgent Help Request") recommendation = "Ignore";
        else recommendation = paymentIntent == "None" ? "Ignore" : "Watch";

        var result = new AiTriageResult
        {
            LanguageCode = DetectLanguage(text),
            IsTechnicalProblem = isTechnical || (payIntent && hasSoftwareContext),
            IsEmergency = isEmergency,
            PaymentIntent = paymentIntent,
            AssistanceRequested = assistanceRequested,
            RejectReason = redFlag.IsRedFlagged ? string.Join("; ", redFlag.Reasons)
                : !hasSoftwareContext ? "No software/system context — not a technology problem."
                : vendorSupport ? "Vendor support request — resolution requires the provider's own team, not a third-party engineer."
                : !isTechnical && !payIntent ? "No technical emergency detected." : null,
            ProblemCategory = payIntent && category == "Non-Urgent Help Request" && hasSoftwareContext
                ? "Non-Urgent Help Request" // honest label; pay-intent keeps it alive downstream
                : category,
            DetectedStack = DetectStack(text).ToList(),
            EstimatedCause = payIntent && !isEmergency
                ? "The company is hiring for ongoing paid development work rather than reporting an incident."
                : BuildCause(category),
            FirstDiagnosticStep = payIntent && !isEmergency
                ? "Review the posting and respond with relevant emergency/contract experience if the engagement fits."
                : BuildStep(category),
            EstimatedFixMinutesMin = isEmergency ? 30 : null,
            EstimatedFixMinutesMax = isEmergency ? 120 : null,
            // Keyword rules are guesses, never judgments: keep confidence strictly below the
            // draft threshold (MinAiConfidenceForDraft, default 0.6) so a heuristic-triaged
            // lead can rank for review but can never auto-generate outreach.
            AiConfidence = (decimal)Math.Clamp(0.35 + request.MatchedTerms.Count * 0.03, 0.3, 0.55),
            OutreachRecommendation = recommendation
        };

        var response = new AiTriageResponse
        {
            Succeeded = true,
            Result = result,
            Provider = Name,
            Model = "heuristic-rules",
            RequestJson = JsonSerializer.Serialize(new { request.Title, request.SourceKey, request.MatchedTerms }),
            ResponseJson = JsonSerializer.Serialize(result)
        };
        return Task.FromResult(response);
    }

    private static string DetectLanguage(string text)
    {
        if (text.Any(c => c is >= '\u3040' and <= '\u30ff')) return "ja";
        if (text.Any(c => c is >= '\uac00' and <= '\ud7af')) return "ko";
        if (text.Any(c => c is >= '\u4e00' and <= '\u9fff')) return "zh";
        if (text.Any(c => c is >= '\u0400' and <= '\u04ff')) return "ru";
        if (text.Any(c => c is >= '\u0600' and <= '\u06ff')) return "ar";

        var padded = " " + text.ToLowerInvariant() + " ";
        static int Hits(string value, string[] words) =>
            words.Count(word => value.Contains(word, StringComparison.Ordinal));
        var candidates = new[]
        {
            (Code: "es", Hits: Hits(padded, new[] { " el ", " la ", " para ", " necesito ", " ayuda ", " con " })),
            (Code: "pt", Hits: Hits(padded, new[] { " para ", " preciso ", " ajuda ", " não ", " uma ", " com " })),
            (Code: "fr", Hits: Hits(padded, new[] { " le ", " la ", " pour ", " besoin ", " avec ", " une " })),
            (Code: "de", Hits: Hits(padded, new[] { " der ", " die ", " das ", " für ", " brauche ", " mit " })),
            (Code: "it", Hits: Hits(padded, new[] { " il ", " la ", " per ", " bisogno ", " aiuto ", " con " }))
        };
        var best = candidates.OrderByDescending(c => c.Hits).First();
        return best.Hits >= 3 ? best.Code : "en";
    }

    private static string Classify(string text)
    {
        foreach (var (keywords, category) in CategoryMap)
            if (keywords.Any(text.Contains))
                return category;
        return "Non-Urgent Help Request";
    }

    private static IEnumerable<string> DetectStack(string text)
    {
        var stack = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        AddIf(text, stack, ".net", ".NET");
        AddIf(text, stack, "asp.net", "ASP.NET Core");
        AddIf(text, stack, "blazor", "Blazor");
        AddIf(text, stack, "iis", "IIS");
        AddIf(text, stack, "azure", "Azure");
        AddIf(text, stack, "sql server", "SQL Server");
        AddIf(text, stack, "postgres", "Postgres");
        AddIf(text, stack, "mysql", "MySQL");
        AddIf(text, stack, "stripe", "Stripe");
        AddIf(text, stack, "shopify", "Shopify");
        AddIf(text, stack, "woocommerce", "WooCommerce");
        AddIf(text, stack, "wordpress", "WordPress");
        AddIf(text, stack, "node", "Node");
        AddIf(text, stack, "php", "PHP");
        AddIf(text, stack, "cloudflare", "Cloudflare");
        AddIf(text, stack, "nginx", "Nginx");
        AddIf(text, stack, "apache", "Apache");
        return stack; // empty when nothing was actually detected — never invent a stack
    }

    private static void AddIf(string text, HashSet<string> stack, string needle, string label)
    {
        if (text.Contains(needle, StringComparison.OrdinalIgnoreCase))
            stack.Add(label);
    }

    private static string BuildCause(string category) => category switch
    {
        "Deployment Failure" => "The application may be failing during startup because of a deployment or configuration change.",
        "Database Failure" => "The database may be unreachable or the connection string may have changed.",
        "Payment/Checkout Failure" => "The payment integration may be failing verification after a key or endpoint change.",
        "DNS/TLS Failure" => "A DNS record or TLS certificate may have expired or been misconfigured.",
        "Website Down" => "The web server or hosting layer may be misconfigured or overloaded.",
        "Production Outage" => "A recent change likely broke a critical production dependency.",
        _ => "Insufficient detail to determine a specific cause without the error output."
    };

    private static string BuildStep(string category) => category switch
    {
        "Deployment Failure" => "Check application startup logs and confirm the production connection string resolves from the host.",
        "Database Failure" => "Confirm the database server is reachable and credentials/firewall rules are intact.",
        "Payment/Checkout Failure" => "Verify the webhook endpoint is reachable and the signing secret matches the live key.",
        "DNS/TLS Failure" => "Check DNS resolution and the TLS certificate expiry for the affected host.",
        "Website Down" => "Confirm the HTTP server is reachable and inspect the 5xx response and recent deploys.",
        _ => "Request the exact error message, hosting environment, and what changed most recently."
    };
}
