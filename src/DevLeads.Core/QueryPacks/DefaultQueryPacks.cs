namespace DevLeads.Core.QueryPacks;

/// <summary>Seed definition for a query pack.</summary>
public sealed record QueryPackSeed(string Name, string Description, bool IsHighPriority, bool IsNegative, string[] Terms);

/// <summary>The built-in query packs from the design document, used to seed the database.</summary>
public static class DefaultQueryPacks
{
    public static readonly QueryPackSeed EmergencyGeneric = new(
        "EmergencyGeneric", "Generic urgent outage / broken-system language", true, false, new[]
        {
            "site down", "website down", "production down", "production issue",
            "urgent developer needed", "need fixed today", "need help asap",
            "client site broken", "checkout not working", "api down", "api stopped working",
            "database down", "deployment failed", "500 error production",
            "502 bad gateway", "503 service unavailable", "dns not resolving", "ssl certificate expired",
            "live site is down", "site is broken", "service outage", "major outage",
            "customers cannot log in", "customers can't log in", "critical bug",
            "production incident", "need emergency help", "broken after deploy",
            "not working in production", "revenue blocked", "business is down"
        });

    public static readonly QueryPackSeed DotNetSqlPriority = new(
        "DotNetSqlPriority", "Preferred-stack emergency terms", true, false, new[]
        {
            "asp.net core production down", ".net app 500 error", "iis 500 error", "iis 500.19",
            "blazor deployment failed", "blazor server disconnected", "ef core migration failed",
            "sql server connection failed", "sql server timeout", "connection string not working",
            "azure app service deployment failed", "app pool crashing", "web.config error",
            "kestrel failed to start", "application pool stopped", "sql login failed",
            "cannot connect to sql server", "azure app service 500", "entity framework migration broke",
            "blazor server app down", "iis site down", "database unavailable"
        });

    public static readonly QueryPackSeed PaymentEcommerce = new(
        "PaymentEcommerce", "Checkout and payment gateway failures", true, false, new[]
        {
            "checkout broken", "stripe webhook failed", "payment gateway not working",
            "orders not coming through", "woocommerce checkout error", "shopify checkout issue",
            "can't take payments", "customers cannot pay", "cart not working",
            "stripe payments failing", "webhook not receiving events", "paypal checkout broken",
            "payment failed in production", "subscription checkout broken",
            "customers cannot checkout", "lost orders", "order confirmation not sending"
        });

    public static readonly QueryPackSeed AgencyClientUrgency = new(
        "AgencyClientUrgency", "Agency overflow and client emergencies", true, false, new[]
        {
            "client site is down", "client needs this fixed", "developer unavailable",
            "need contractor immediately", "production bug for client", "agency needs help",
            "urgent freelancer developer", "client emergency", "white label emergency",
            "need senior developer today", "contractor for outage", "freelancer for production issue"
        });

    public static readonly QueryPackSeed SaaSApiAuth = new(
        "SaaSApiAuth", "SaaS outages, API failures, webhooks, and login emergencies", true, false, new[]
        {
            "api returning 500", "api requests failing", "webhook failing", "webhooks stopped",
            "login broken", "users cannot login", "users can't login", "oauth error",
            "sso not working", "token validation failed", "auth outage",
            "background jobs stuck", "queue backed up", "cron job failed",
            "emails not sending", "transactional email down"
        });

    public static readonly QueryPackSeed InfraOps = new(
        "InfraOps", "Hosting, DNS, TLS, CDN, database, and reliability incidents", true, false, new[]
        {
            "server down", "hosting down", "database connection failed", "database locked",
            "connection timeout", "request timeout", "timeout in production",
            "cloudflare error", "cloudflare 522", "cloudflare 525",
            "nginx 502", "apache 500", "certificate expired", "ssl expired",
            "dns propagation issue", "mx records broken", "email domain not working",
            "cpu at 100", "memory leak production", "out of disk space"
        });

    public static readonly QueryPackSeed WordPressHosting = new(
        "WordPressHosting", "Common commercial site failures in WordPress and managed hosting", true, false, new[]
        {
            "wordpress site down", "wordpress white screen", "critical error on this website",
            "woocommerce checkout broken", "plugin update broke site", "theme update broke site",
            "wp admin inaccessible", "elementor site broken", "fatal error wordpress"
        });

    public static readonly QueryPackSeed ContractProjectWork = new(
        "ContractProjectWork", "Paid contract, freelance, and short-term implementation signals", true, false, new[]
        {
            "contract developer", "freelance developer", "freelancer needed", "contractor needed",
            "short term contract", "part-time contractor", "project-based", "fixed price",
            "hourly contract", "consultant needed", "implementation partner", "integration project",
            "migration project", "backend contractor", "frontend contractor", "full-stack contractor",
            "shopify developer needed", "wordpress developer needed", "web developer needed",
            "api integration", "database migration", "automation project", "production support",
            "maintenance contract", "support retainer", "overflow work", "white label developer"
        });

    public static readonly QueryPackSeed SupportPain = new(
        "SupportPain", "Public support issues that may indicate urgent commercial repair work", true, false, new[]
        {
            "not working", "stopped working", "broken", "error after update", "fatal error",
            "critical error", "white screen", "cannot access admin", "locked out",
            "orders missing", "checkout error", "payment error", "email not sending",
            "smtp error", "webhook not firing", "redirect loop", "too many redirects",
            "mixed content", "certificate problem", "database error", "plugin conflict",
            "theme conflict", "after migration", "after update", "site health critical"
        });

    public static readonly QueryPackSeed HireIntent = new(
        "HireIntent", "Explicit willingness-to-pay / hire-me-now language", true, false, new[]
        {
            "willing to pay", "will pay for help", "happy to pay", "paid task",
            "paid gig", "paid help needed", "looking to hire", "need to hire",
            "hire a developer", "hire someone to fix", "budget for this",
            "what would you charge", "how much would it cost", "cost to fix",
            "name your price", "send me a quote", "hourly rate", "seeking freelancer",
            "need freelancer", "developer needed urgently", "dev needed asap",
            "pay someone to fix", "paying for a fix", "emergency developer"
        });

    public static readonly QueryPackSeed PaidFeatureRequest = new(
        "PaidFeatureRequest", "Feature requests with sponsorship/bounty/pay language", true, false, new[]
        {
            "sponsor this feature", "sponsor development", "fund this feature",
            "willing to sponsor", "paid feature request", "bounty for", "offer a bounty",
            "put a bounty", "pay for this feature", "would pay for this",
            "implement this for a fee", "happy to sponsor", "willing to fund"
        });

    public static readonly QueryPackSeed DotNetModernization = new(
        "DotNetModernization", "Legacy .NET / enterprise application modernization consulting signals", true, false, new[]
        {
            // Legacy .NET estate (short terms double as HN Algolia queries).
            "legacy .net", ".net framework migration", ".net framework 4", ".net 4.8",
            "migrate to .net", ".net migration", ".net upgrade", "upgrade .net framework",
            "framework to core", "asp.net mvc migration", "webforms", "web forms migration",
            "wcf migration", "wcf service", "winforms migration", "vb.net migration", "vb6",
            "classic asp", "silverlight migration", "crystal reports",
            // Modernization / replatforming engagements.
            ".net modernization", "application modernization", "app modernization",
            "legacy modernization", "modernize legacy", "modernization project",
            "modernization consultant", "legacy application", "legacy codebase",
            "legacy system rewrite", "rewrite legacy", "replatform", "re-platform",
            "monolith migration", "azure migration", "cloud migration",
            "sql server upgrade", "sql server migration", "database modernization",
            // Why enterprises pay for it.
            "end of life framework", "unsupported framework", "out of support",
            "technical debt reduction", "migration consultant", "migration project"
        });

    public static readonly QueryPackSeed AiAutomationProjects = new(
        "AiAutomationProjects", "Commercial AI integration and business-automation project signals", true, false, new[]
        {
            // Concrete implementation work, intentionally avoiding bare "AI" / "automation"
            // terms that would mostly collect news, product promotion, and tutorials.
            "ai automation project", "ai integration project", "llm integration",
            "openai integration", "anthropic integration", "rag implementation",
            "rag pipeline", "build an ai agent", "ai agent development",
            "custom ai agent", "custom chatbot", "chatbot development",
            "document processing ai", "ai document processing", "ai proof of concept",
            "machine learning consultant", "ai consultant needed", "ai developer needed",
            "llm developer", "automation consultant", "automation developer",
            // Business workflow platforms and outcomes that commonly become paid projects.
            "workflow automation", "business process automation", "automate our workflow",
            "automate my workflow", "automate our business", "automate my business",
            "n8n automation", "n8n developer", "zapier automation", "zapier expert",
            "make.com automation", "make.com expert", "power automate consultant",
            "crm automation", "sales automation", "support automation",
            "email automation", "data extraction automation", "api automation"
        });

    public static readonly QueryPackSeed NegativeExclusions = new(
        "NegativeExclusions", "Low-commercial-value / educational exclusions", false, true, new[]
        {
            "homework", "assignment", "class project", "learning project", "just curious",
            "interview question", "leetcode", "toy project", "portfolio project",
            "no budget", "free help", "can someone explain", "beginner question",
            "for school", "student project", "practice project", "looking for mentor",
            "tutorial", "sample app", "demo app", "side project", "open source maintainer wanted",
            // Advice-seekers and non-clients: hobby posts, career questions, and
            // freelancers advertising themselves ([for hire] / "seeking work").
            "hobby project", "personal project", "my first website", "as a beginner",
            "noob question", "eli5", "career advice", "resume review", "cv review",
            "which framework should i learn", "learning roadmap", "is it worth learning",
            "[for hire]", "seeking work", "available for work", "am i the only one"
        });

    public static readonly QueryPackSeed[] All =
    {
        EmergencyGeneric, DotNetSqlPriority, PaymentEcommerce, AgencyClientUrgency,
        SaaSApiAuth, InfraOps, WordPressHosting, ContractProjectWork, SupportPain,
        HireIntent, PaidFeatureRequest, DotNetModernization, AiAutomationProjects,
        NegativeExclusions
    };
}
