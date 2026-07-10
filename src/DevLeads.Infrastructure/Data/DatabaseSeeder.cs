using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using DevLeads.Core;
using DevLeads.Core.Entities;
using DevLeads.Core.QueryPacks;

namespace DevLeads.Infrastructure.Data;

/// <summary>
/// Creates the database and seeds query packs, source configs, and settings.
/// Also migrates older databases: removes retired sources (GitHub Issues) and purges
/// leads that cannot lead to paid work. Never seeds demo/sample leads.
/// </summary>
public static class DatabaseSeeder
{
    public static async Task InitializeAsync(DevLeadsDbContext db, CancellationToken ct = default)
    {
        await db.Database.EnsureCreatedAsync(ct);
        await ApplySchemaUpgradesAsync(db, ct);
        await MigrateAiProviderDefaultsAsync(db, ct);

        var retired = await RemoveRetiredSourcesAsync(db, ct);
        var replaced = await RemoveReplacedSourceConfigsAsync(db, ct);

        if (!await db.OperatorSettings.AnyAsync(ct))
            db.OperatorSettings.Add(new OperatorSettings { Id = 1 });

        await SeedQueryPacksAsync(db, ct);
        await SeedSkillsAsync(db, ct);
        var migrated = await SeedSourceConfigsAsync(db, ct);
        await db.SaveChangesAsync(ct);

        // One-time when the source lineup changes: leads gathered under the old,
        // lower-quality configuration are stale — drop everything still in triage
        // stages that the operator never engaged with.
        if (retired || replaced || migrated)
            await PurgeStaleDiscoveryLeadsAsync(db, ct);

        await PurgeNonActionableLeadsAsync(db, ct);
        await PurgeNonHirableVendorSupportLeadsAsync(db, ct);
        await PurgeSourceLessLeadsAsync(db, ct);
    }

    /// <summary>
    /// EnsureCreated never alters existing tables, so columns added after first release
    /// are applied here with idempotent ALTERs (SQLite raises on duplicates — ignored).
    /// </summary>
    private static async Task ApplySchemaUpgradesAsync(DevLeadsDbContext db, CancellationToken ct)
    {
        var upgrades = new[]
        {
            "ALTER TABLE OperatorSettings ADD COLUMN OpenCodeCliPath TEXT NOT NULL DEFAULT 'opencode'",
            "ALTER TABLE Opportunities ADD COLUMN PaymentIntent TEXT NOT NULL DEFAULT ''",
            "ALTER TABLE Opportunities ADD COLUMN AssistanceRequested INTEGER NULL",
            "ALTER TABLE Opportunities ADD COLUMN FeeIsEstimate INTEGER NOT NULL DEFAULT 1",
            // EnsureCreated never adds tables to an existing database either.
            """
            CREATE TABLE IF NOT EXISTS "Skills" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_Skills" PRIMARY KEY AUTOINCREMENT,
                "Name" TEXT NOT NULL,
                "Category" TEXT NOT NULL,
                "Weight" INTEGER NOT NULL,
                "Enabled" INTEGER NOT NULL,
                "Aliases" TEXT NOT NULL
            )
            """
        };
        foreach (var sql in upgrades)
        {
            try { await db.Database.ExecuteSqlRawAsync(sql, ct); }
            catch (Microsoft.Data.Sqlite.SqliteException) { /* column already exists */ }
        }
    }

    /// <summary>
    /// Moves settings still on an old AI default onto the current one (OpenCode CLI).
    /// Explicit operator choices (anything not matching an old default) are untouched.
    /// </summary>
    private static async Task MigrateAiProviderDefaultsAsync(DevLeadsDbContext db, CancellationToken ct)
    {
        var settings = await db.OperatorSettings.FirstOrDefaultAsync(ct);
        if (settings is null) return;

        var wasOldDefaultProvider = settings.AiProvider is "Heuristic";
        if (wasOldDefaultProvider)
        {
            settings.AiProvider = "OpenCode";
            if (settings.AiModel is "claude-opus-4-8" or "" or "opencode/deepseek-v4-flash-free")
                settings.AiModel = OperatorSettings.DefaultOpenCodeModel;
            if (settings.AiTimeoutSeconds < 120) settings.AiTimeoutSeconds = 120;
            await db.SaveChangesAsync(ct);
        }
    }

    /// <summary>Display-style pack names used before packs were keyed by stable identifiers.</summary>
    private static readonly Dictionary<string, string> LegacyPackNames = new()
    {
        [".NET / SQL priority stack"] = "DotNetSqlPriority",
        ["Payment / e-commerce"] = "PaymentEcommerce",
        ["Agency / client urgency"] = "AgencyClientUrgency",
        ["SaaS / API / auth"] = "SaaSApiAuth",
        ["Infrastructure / hosting"] = "InfraOps",
        ["WordPress / hosting"] = "WordPressHosting",
        ["Contract / project work"] = "ContractProjectWork",
        ["Support pain"] = "SupportPain",
    };

    private static async Task SeedQueryPacksAsync(DevLeadsDbContext db, CancellationToken ct)
    {
        // Rename packs stored under the old display names so source QueryPacksCsv
        // references (stable keys) resolve and no duplicate packs get seeded.
        foreach (var pack in await db.QueryPacks.ToListAsync(ct))
            if (LegacyPackNames.TryGetValue(pack.Name, out var newName))
                pack.Name = newName;
        // Persist renames now — the upsert loop below queries the database by name.
        await db.SaveChangesAsync(ct);

        foreach (var seed in DefaultQueryPacks.All)
        {
            var existing = await db.QueryPacks.FirstOrDefaultAsync(q => q.Name == seed.Name, ct);
            if (existing is null)
            {
                db.QueryPacks.Add(new QueryPack
                {
                    Name = seed.Name,
                    Description = seed.Description,
                    IsHighPriority = seed.IsHighPriority,
                    IsNegative = seed.IsNegative,
                    Terms = string.Join('\n', seed.Terms)
                });
                continue;
            }

            existing.Description = seed.Description;
            existing.IsHighPriority = seed.IsHighPriority;
            existing.IsNegative = seed.IsNegative;
            existing.Terms = string.Join('\n', seed.Terms);
        }
    }

    /// <summary>Seeds the operator skill profile once; the Skills page owns it afterwards.</summary>
    private static async Task SeedSkillsAsync(DevLeadsDbContext db, CancellationToken ct)
    {
        if (await db.Skills.AnyAsync(ct)) return;
        db.Skills.AddRange(DevLeads.Core.Skills.DefaultSkills.All.Select(s => new Skill
        {
            Name = s.Name, Category = s.Category, Weight = s.Weight, Enabled = s.Enabled, Aliases = s.Aliases
        }));
        await db.SaveChangesAsync(ct);
    }

    private static async Task<bool> SeedSourceConfigsAsync(DevLeadsDbContext db, CancellationToken ct)
    {
        var migrated = false;
        foreach (var seed in DefaultSources())
        {
            var existing = await db.SourceConfigs.FirstOrDefaultAsync(s => s.SourceKey == seed.SourceKey, ct);
            if (existing is null)
            {
                db.SourceConfigs.Add(seed);
                migrated = true;
                continue;
            }

            if (IsLegacyDefaultSource(existing))
                migrated |= ApplySourceDefaults(existing, seed);
        }
        return migrated;
    }

    /// <summary>Detects configs still carrying earlier seeded defaults so we can upgrade them in place.</summary>
    private static bool IsLegacyDefaultSource(SourceConfig source) =>
        source.QueryPacksCsv == "EmergencyGeneric,DotNetSqlPriority" ||
        source.ParametersJson is "{\"labels\":\"bug\"}" or "{\"subreddits\":\"webdev;dotnet;sysadmin\"}" ||
        (source.SourceKey == "hackernews" && source.ParametersJson.Contains("hnrss.org", StringComparison.OrdinalIgnoreCase)) ||
        source.ParametersJson.Contains("\"tags\":\"asp.net-core;sql-server;iis\"", StringComparison.OrdinalIgnoreCase) ||
        source.ParametersJson.Contains("\"site\":\"stackoverflow\"", StringComparison.OrdinalIgnoreCase) ||
        source.ParametersJson.Contains("webdev;dotnet;sysadmin;msp;devops;azure;wordpress;shopify", StringComparison.OrdinalIgnoreCase) ||
        source.SourceKey is "remotive" or "rss_jobs" or "rss_support" or "reddit_hiring" or "hn_hiring"
            or "reddit_wordpress_shopify" or "reddit_webdev_ops" or "reddit_business_ecommerce"
            or "reddit_stacks" or "reddit_saas_tools" or "hackernews" or "stackexchange_radar"
            or "bounties_opire" or "github_bounties" or "github_feature_requests";

    /// <summary>
    /// Reapplies seeded defaults, returning whether anything actually changed — a boot with
    /// unchanged defaults must NOT count as a migration (that would purge + re-triage all
    /// discovery leads on every restart and waste the AI budget).
    /// </summary>
    private static bool ApplySourceDefaults(SourceConfig target, SourceConfig seed)
    {
        var changed =
            target.DisplayName != seed.DisplayName ||
            target.Enabled != seed.Enabled ||
            target.PollIntervalMinutes != seed.PollIntervalMinutes ||
            target.MaxItemsPerRun != seed.MaxItemsPerRun ||
            target.QueryPacksCsv != seed.QueryPacksCsv ||
            target.ParametersJson != seed.ParametersJson ||
            target.MinPreFilterScore != seed.MinPreFilterScore ||
            target.MinOpportunityScore != seed.MinOpportunityScore ||
            target.DraftThreshold != seed.DraftThreshold ||
            target.AlertThreshold != seed.AlertThreshold ||
            target.AutoModeEligible != seed.AutoModeEligible;
        if (!changed) return false;

        target.DisplayName = seed.DisplayName;
        target.Enabled = seed.Enabled;
        target.PollIntervalMinutes = seed.PollIntervalMinutes;
        target.MaxItemsPerRun = seed.MaxItemsPerRun;
        target.QueryPacksCsv = seed.QueryPacksCsv;
        target.ParametersJson = seed.ParametersJson;
        target.MinPreFilterScore = seed.MinPreFilterScore;
        target.MinOpportunityScore = seed.MinOpportunityScore;
        target.DraftThreshold = seed.DraftThreshold;
        target.AlertThreshold = seed.AlertThreshold;
        target.AutoModeEligible = seed.AutoModeEligible;
        return true;
    }

    /// <summary>
    /// Default sources are chosen for commercial intent: places where a business owner,
    /// manager, or agency is describing a problem they are prepared to pay to solve.
    /// </summary>
    private static IEnumerable<SourceConfig> DefaultSources() => new[]
    {
        // Companies posting paid contract/freelance software engagements.
        new SourceConfig { SourceKey = "remotive", DisplayName = "Remotive — paid remote software roles", Enabled = true,
            PollIntervalMinutes = 60, MaxItemsPerRun = 60,
            QueryPacksCsv = "EmergencyGeneric,DotNetSqlPriority,PaymentEcommerce,SaaSApiAuth,InfraOps,ContractProjectWork,HireIntent",
            MinPreFilterScore = 1, MinOpportunityScore = 42,
            ParametersJson = "{\"category\":\"software-dev\",\"jobTypes\":\"any\"}" },

        // Job boards, paid-engagement feeds, and public support communities. Job posts
        // prove budget; support feeds surface urgent WordPress/Shopify issues for manual review.
        new SourceConfig { SourceKey = "rss_jobs", DisplayName = "RSS jobs — paid engineering work", Enabled = true,
            PollIntervalMinutes = 60, MaxItemsPerRun = 160,
            QueryPacksCsv = "EmergencyGeneric,DotNetSqlPriority,PaymentEcommerce,SaaSApiAuth,InfraOps,ContractProjectWork,HireIntent",
            MinPreFilterScore = 1, MinOpportunityScore = 42,
            ParametersJson = RssParams("21", new[] {
                // We Work Remotely: companies pay to post; engineering and infra verticals.
                "https://weworkremotely.com/remote-jobs.rss",
                "https://weworkremotely.com/categories/remote-programming-jobs.rss",
                "https://weworkremotely.com/categories/remote-full-stack-programming-jobs.rss",
                "https://weworkremotely.com/categories/remote-back-end-programming-jobs.rss",
                "https://weworkremotely.com/categories/remote-front-end-programming-jobs.rss",
                "https://weworkremotely.com/categories/remote-devops-sysadmin-jobs.rss",

                // RemoteOK: high-volume paid remote engineering postings.
                "https://remoteok.com/remote-dev-jobs.rss",
                "https://remoteok.com/remote-c-sharp-jobs.rss",
                "https://remoteok.com/remote-php-jobs.rss",
                "https://remoteok.com/remote-python-jobs.rss",
                "https://remoteok.com/remote-react-jobs.rss",

                // Jobicy: public RSS/API, contract engineering and infrastructure filters.
                "https://jobicy.com/jobs/feed?industry=software-engineering&type=contract",
                "https://jobicy.com/jobs/feed?industry=devops-infrastructure&type=contract",
                "https://jobicy.com/jobs/feed?industry=e-commerce&type=contract",
                "https://jobicy.com/jobs/feed?industry=qa-testing&type=contract",

                // Remote First Jobs: public skill/category RSS feeds, including contract work.
                "https://remotefirstjobs.com/rss/jobs/software-development.rss",
                "https://remotefirstjobs.com/rss/jobs/dotnet.rss",
                "https://remotefirstjobs.com/rss/jobs/devops.rss",
                "https://remotefirstjobs.com/rss/jobs/wordpress.rss",
                "https://remotefirstjobs.com/rss/jobs/php.rss",
                "https://remotefirstjobs.com/rss/jobs/contract.rss",

                // Real Work From Anywhere: public engineering category feeds.
                "https://www.realworkfromanywhere.com/remote-fullstack-jobs/rss.xml",
                "https://www.realworkfromanywhere.com/remote-backend-jobs/rss.xml",
                "https://www.realworkfromanywhere.com/remote-devops-and-sysadmin-jobs/rss.xml",
                "https://www.realworkfromanywhere.com/remote-software-developer-jobs/rss.xml" }) },

        // Advice-heavy: posters rarely hire. Kept as a radar, but polled less often and
        // held to a higher bar — only pain with commercial/pay signals survives scoring.
        // Spans ~25 forums across the whole stack (CMS, e-commerce, infra, auth, payments,
        // automation, frameworks) so leads aren't dominated by WordPress/Shopify.
        // MinPreFilterScore 6: vendor forums are the noisiest source — posts addressed to
        // the product's own support team are rejected at triage, and the higher bar keeps
        // marginal single-keyword posts out entirely.
        new SourceConfig { SourceKey = "rss_support", DisplayName = "RSS support — multi-tech forum radar", PollIntervalMinutes = 30, MaxItemsPerRun = 240,
            QueryPacksCsv = "EmergencyGeneric,PaymentEcommerce,SaaSApiAuth,InfraOps,WordPressHosting,SupportPain,PaidFeatureRequest",
            AutoModeEligible = false, MinPreFilterScore = 6, MinOpportunityScore = 46,
            ParametersJson = RssParams("14", new[] {
                // CMS / e-commerce
                "https://wordpress.org/support/forum/how-to-and-troubleshooting/feed/",
                "https://wordpress.org/support/plugin/woocommerce/feed/",
                "https://wordpress.org/support/plugin/elementor/feed/",
                "https://community.shopify.dev/latest.rss",
                "https://forum.ghost.org/latest.rss",
                "https://forum.strapi.io/latest.rss",
                // Infra / hosting / delivery
                "https://community.letsencrypt.org/latest.rss",
                "https://community.fly.io/latest.rss",
                "https://answers.netlify.com/latest.rss",
                "https://forums.docker.com/latest.rss",
                "https://discuss.kubernetes.io/latest.rss",
                "https://discuss.hashicorp.com/latest.rss",
                "https://forum.gitlab.com/latest.rss",
                "https://community.grafana.com/latest.rss",
                "https://discuss.elastic.co/latest.rss",
                // Auth / APIs / AI
                "https://community.auth0.com/latest.rss",
                "https://community.openai.com/latest.rss",
                // Frameworks
                "https://forum.djangoproject.com/latest.rss",
                "https://discuss.rubyonrails.org/latest.rss",
                // Low/no-code + automation (business owners, real budgets)
                "https://forum.bubble.io/latest.rss",
                "https://community.retool.com/latest.rss",
                "https://community.n8n.io/latest.rss",
                "https://community.make.com/latest.rss",
                "https://community.monday.com/latest.rss" }) },

        // Hiring subreddits: posts tagged [Hiring] are people offering to pay right now.
        new SourceConfig { SourceKey = "reddit_hiring", DisplayName = "Reddit hiring — paid task posts", Enabled = true,
            PollIntervalMinutes = 30, MaxItemsPerRun = 75,
            QueryPacksCsv = "EmergencyGeneric,DotNetSqlPriority,PaymentEcommerce,SaaSApiAuth,InfraOps,AgencyClientUrgency,ContractProjectWork,HireIntent",
            AutoModeEligible = false, MinPreFilterScore = 1, MinOpportunityScore = 42,
            ParametersJson = "{\"connector\":\"reddit\",\"subreddits\":\"forhire;hiring;jobbit;hireawebdeveloper;hireaprogrammer;freelance_forhire;devopsjobs;sysadminjobs\",\"daysBack\":\"10\",\"requireHiring\":\"true\"}" },

        // Bounty platforms: money is already attached to the work. Connector-side
        // skill filtering keeps only bounties that touch the operator's profile.
        new SourceConfig { SourceKey = "bounties_opire", DisplayName = "Opire — open bounties on GitHub issues", Enabled = true,
            PollIntervalMinutes = 240, MaxItemsPerRun = 40,
            QueryPacksCsv = "HireIntent,PaidFeatureRequest,ContractProjectWork",
            AutoModeEligible = false, MinPreFilterScore = 1, MinOpportunityScore = 40,
            ParametersJson = "{\"connector\":\"opire\",\"maxPages\":\"4\",\"minAmountUsd\":\"20\"}" },

        // GitHub bounty ecosystem: BountyHub, Algora, IssueHunt etc. all anchor bounties
        // to public GitHub issues — one search net catches them all, skill-filtered.
        new SourceConfig { SourceKey = "github_bounties", DisplayName = "GitHub bounties — BountyHub / Algora / IssueHunt", Enabled = true,
            PollIntervalMinutes = 120, MaxItemsPerRun = 60,
            QueryPacksCsv = "HireIntent,PaidFeatureRequest,ContractProjectWork",
            AutoModeEligible = false, MinPreFilterScore = 1, MinOpportunityScore = 40,
            // '*' = the query already encodes fit (language:) or is a tiny platform net,
            // so its results skip the per-item skill-text filter. Bounties stay open for
            // months, hence the wide daysBack.
            ParametersJson = JsonSerializer.Serialize(new
            {
                connector = "github_search",
                daysBack = "365", requireSkillMatch = "true",
                queries = string.Join('\n',
                    "*label:bounty language:C#",
                    "*label:bounty language:TypeScript",
                    "*label:\"💎 Bounty\" language:C#",   // Algora's standard bounty label
                    "*bountyhub.dev in:body,comments",     // BountyHub (~all volume, it's small)
                    "label:\"💵 Funded on Issuehunt\"",
                    "label:bounty")
            }) },

        // Feature requests where the poster offers money: "willing to pay", sponsorships,
        // ad-hoc bounties in titles. AI-triaged so the pay-intent judgment is real.
        new SourceConfig { SourceKey = "github_feature_requests", DisplayName = "GitHub feature requests — willing-to-pay", Enabled = true,
            PollIntervalMinutes = 180, MaxItemsPerRun = 60,
            QueryPacksCsv = "HireIntent,PaidFeatureRequest",
            AutoModeEligible = false, MinPreFilterScore = 1, MinOpportunityScore = 42,
            ParametersJson = JsonSerializer.Serialize(new
            {
                connector = "github_search",
                daysBack = "60", requireSkillMatch = "true",
                queries = string.Join('\n',
                    "*\"willing to pay\" language:C#",
                    "*\"would pay for\" language:C#",
                    "*\"willing to pay\" language:TypeScript",
                    "\"willing to pay\"",
                    "\"would pay for\"",
                    "bounty in:title")
            }) },

        // Hacker News hiring threads: "Who is hiring?" job comments and the monthly
        // freelancer thread, where "SEEKING FREELANCER" entries are clients with budget.
        new SourceConfig { SourceKey = "hn_hiring", DisplayName = "HN hiring — who-is-hiring + freelance threads", Enabled = true,
            PollIntervalMinutes = 60, MaxItemsPerRun = 80,
            QueryPacksCsv = "EmergencyGeneric,DotNetSqlPriority,PaymentEcommerce,SaaSApiAuth,InfraOps,ContractProjectWork,HireIntent",
            AutoModeEligible = false, MinPreFilterScore = 1, MinOpportunityScore = 42,
            ParametersJson = RssParams("30", new[] {
                "https://hnrss.org/whoishiring/jobs",
                "https://hnrss.org/whoishiring/freelance" }) },

        // Topical communities: search recent posts for urgent technical/business pain.
        // Advice-heavy — higher score bar so only commercially-signaled pain survives.
        new SourceConfig { SourceKey = "reddit_wordpress_shopify", DisplayName = "Reddit pain — ecommerce + CMS platforms", PollIntervalMinutes = 30, MaxItemsPerRun = 120,
            QueryPacksCsv = "EmergencyGeneric,DotNetSqlPriority,PaymentEcommerce,SaaSApiAuth,InfraOps,WordPressHosting,SupportPain",
            AutoModeEligible = false, MinPreFilterScore = 5, MinOpportunityScore = 46,
            ParametersJson = "{\"connector\":\"reddit\",\"subreddits\":\"wordpress;woocommerce;shopify;magento;bigcommerce;squarespace;wix;webflow;prestashop;ecommerce\",\"daysBack\":\"14\",\"requireHiring\":\"false\"}" },

        new SourceConfig { SourceKey = "reddit_webdev_ops", DisplayName = "Reddit pain — webdev, cloud + ops", PollIntervalMinutes = 30, MaxItemsPerRun = 120,
            QueryPacksCsv = "EmergencyGeneric,DotNetSqlPriority,PaymentEcommerce,SaaSApiAuth,InfraOps,SupportPain",
            AutoModeEligible = false, MinPreFilterScore = 6, MinOpportunityScore = 46,
            ParametersJson = "{\"connector\":\"reddit\",\"subreddits\":\"webdev;sysadmin;devops;webhosting;dotnet;aws;AZURE;googlecloud;kubernetes;docker;selfhosted;vps\",\"daysBack\":\"14\",\"requireHiring\":\"false\"}" },

        // Language/framework/database communities — broad technology coverage.
        new SourceConfig { SourceKey = "reddit_stacks", DisplayName = "Reddit pain — languages, frameworks + databases", PollIntervalMinutes = 45, MaxItemsPerRun = 120,
            QueryPacksCsv = "EmergencyGeneric,DotNetSqlPriority,SaaSApiAuth,InfraOps,SupportPain",
            AutoModeEligible = false, MinPreFilterScore = 6, MinOpportunityScore = 46,
            ParametersJson = "{\"connector\":\"reddit\",\"subreddits\":\"PHP;laravel;node;reactjs;nextjs;django;flask;rails;golang;java;csharp;sqlserver;PostgreSQL;mysql;mongodb\",\"daysBack\":\"14\",\"requireHiring\":\"false\"}" },

        // Business-tool communities: non-developers whose integrations broke — they hire.
        new SourceConfig { SourceKey = "reddit_saas_tools", DisplayName = "Reddit pain — SaaS + business tools", PollIntervalMinutes = 60, MaxItemsPerRun = 80,
            QueryPacksCsv = "EmergencyGeneric,PaymentEcommerce,SaaSApiAuth,InfraOps,SupportPain,HireIntent",
            AutoModeEligible = false, MinPreFilterScore = 5, MinOpportunityScore = 44,
            ParametersJson = "{\"connector\":\"reddit\",\"subreddits\":\"stripe;salesforce;hubspot;zapier;airtable;quickbooks;mailchimp;twilio\",\"daysBack\":\"14\",\"requireHiring\":\"false\"}" },

        new SourceConfig { SourceKey = "reddit_business_ecommerce", DisplayName = "Reddit pain — business + ecommerce", PollIntervalMinutes = 30, MaxItemsPerRun = 100,
            QueryPacksCsv = "EmergencyGeneric,PaymentEcommerce,SaaSApiAuth,InfraOps,SupportPain,HireIntent",
            AutoModeEligible = false, MinPreFilterScore = 5, MinOpportunityScore = 44,
            ParametersJson = "{\"connector\":\"reddit\",\"subreddits\":\"smallbusiness;Entrepreneur;ecommerce;SaaS;startups;agency\",\"daysBack\":\"14\",\"requireHiring\":\"false\"}" },

        // Founders/operators posting real production pain and "need help now" asks.
        // HireIntent terms make the Algolia search actively hunt hire/pay language.
        new SourceConfig { SourceKey = "hackernews", DisplayName = "Hacker News — founder/operator pain", PollIntervalMinutes = 15, MaxItemsPerRun = 50,
            QueryPacksCsv = "EmergencyGeneric,PaymentEcommerce,SaaSApiAuth,InfraOps,ContractProjectWork,SupportPain,HireIntent",
            MinPreFilterScore = 1, MinOpportunityScore = 40,
            ParametersJson = "{\"daysBack\":\"14\"}" },

        // Lowest pay-intent of all sources (askers want free answers): strict bar, radar only.
        // The anonymous SE API quota is 300 requests/day for the whole IP — at 18 sites x 2
        // terms per run, a 180-minute poll keeps us at ~288/day.
        new SourceConfig { SourceKey = "stackexchange_radar", DisplayName = "Stack Exchange radar — production issues", Enabled = true,
            PollIntervalMinutes = 180, MaxItemsPerRun = 80,
            QueryPacksCsv = "EmergencyGeneric,DotNetSqlPriority,PaymentEcommerce,SaaSApiAuth,InfraOps,WordPressHosting,SupportPain",
            AutoModeEligible = false,
            ParametersJson = "{\"connector\":\"stackexchange\",\"sites\":\"serverfault;stackoverflow;dba;webmasters;wordpress;drupal;magento;joomla;craftcms;sharepoint;salesforce;webapps;devops;security;networkengineering;askubuntu;unix;superuser\",\"daysBack\":\"5\"}",
            MinPreFilterScore = 8, MinOpportunityScore = 48 },
    };

    // Single overload on purpose: a (daysBack, params feeds) / (daysBack, provider, params feeds)
    // pair is ambiguous — C# bound the first FEED as the provider, silently dropping the feed
    // and disabling AI triage for the source.
    private static string RssParams(string daysBack, string[] feeds, string? triageProvider = null)
    {
        var p = new Dictionary<string, object> { ["connector"] = "rss", ["daysBack"] = daysBack, ["feeds"] = feeds };
        if (triageProvider is not null) p["triageProvider"] = triageProvider;
        return JsonSerializer.Serialize(p);
    }

    /// <summary>Deletes retired source configs and every item/lead they produced (e.g. GitHub Issues).</summary>
    private static async Task<bool> RemoveRetiredSourcesAsync(DevLeadsDbContext db, CancellationToken ct)
    {
        string[] retired = { "github" };

        var configs = await db.SourceConfigs.Where(s => retired.Contains(s.SourceKey)).ToListAsync(ct);
        if (configs.Count > 0) db.SourceConfigs.RemoveRange(configs);

        var rawItems = await db.RawSourceItems.Where(r => retired.Contains(r.SourceKey)).ToListAsync(ct);
        if (rawItems.Count > 0) db.RawSourceItems.RemoveRange(rawItems);

        var opportunities = await db.Opportunities.Where(o => retired.Contains(o.SourceKey)).ToListAsync(ct);
        if (opportunities.Count > 0) db.Opportunities.RemoveRange(opportunities);

        var changed = configs.Count + rawItems.Count + opportunities.Count > 0;
        if (changed) await db.SaveChangesAsync(ct);
        return changed;
    }

    /// <summary>Removes old broad source config rows after splitting them into tuned variants.</summary>
    private static async Task<bool> RemoveReplacedSourceConfigsAsync(DevLeadsDbContext db, CancellationToken ct)
    {
        string[] replaced = { "rss", "reddit", "stackexchange", "reddit_pain" };

        var configs = await db.SourceConfigs.Where(s => replaced.Contains(s.SourceKey)).ToListAsync(ct);
        if (configs.Count == 0) return false;

        db.SourceConfigs.RemoveRange(configs);
        await db.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>Statuses meaning the operator has actually engaged with the lead — never auto-purge these.</summary>
    private static readonly OpportunityStatus[] EngagedStatuses =
    {
        OpportunityStatus.Approved, OpportunityStatus.Contacted, OpportunityStatus.Responded,
        OpportunityStatus.Qualified, OpportunityStatus.QuoteDrafted, OpportunityStatus.QuoteSent,
        OpportunityStatus.Accepted, OpportunityStatus.InProgress, OpportunityStatus.Fixed,
        OpportunityStatus.PaymentPending, OpportunityStatus.Paid, OpportunityStatus.Won,
        OpportunityStatus.Lost
    };

    /// <summary>
    /// One-time after a source-lineup migration: leads still sitting in triage stages were
    /// collected under the old, lower-quality configuration — drop them (manual entries
    /// and operator-engaged leads are always kept).
    /// </summary>
    private static async Task PurgeStaleDiscoveryLeadsAsync(DevLeadsDbContext db, CancellationToken ct)
    {
        var stale = await db.Opportunities
            .Where(o => o.SourceKey != "manual" && !EngagedStatuses.Contains(o.Status))
            .ToListAsync(ct);

        // Unlike the ongoing junk purge, migration also clears the dedup history for
        // untouched discovery items so posts get re-ingested and re-triaged under the
        // new source configuration and rules.
        var engagedIds = await db.Opportunities
            .Where(o => EngagedStatuses.Contains(o.Status))
            .Select(o => o.Id).ToListAsync(ct);
        var staleRaws = await db.RawSourceItems
            .Where(r => r.SourceKey != "manual" &&
                        (r.OpportunityId == null || !engagedIds.Contains(r.OpportunityId.Value)))
            .ToListAsync(ct);
        if (staleRaws.Count > 0) db.RawSourceItems.RemoveRange(staleRaws);

        if (stale.Count > 0) db.Opportunities.RemoveRange(stale);
        if (stale.Count + staleRaws.Count > 0) await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Purges leads that will not lead to financial compensation: pre-filter rejects,
    /// triage rejects, do-not-contact posts, and non-urgent/irrelevant help requests.
    /// Leads from pay-intent sources (job boards, hiring subs) are kept unless dead —
    /// a non-urgent contract posting is still paid work.
    /// </summary>
    private static async Task PurgeNonActionableLeadsAsync(DevLeadsDbContext db, CancellationToken ct)
    {
        var deadStatuses = new[]
        {
            OpportunityStatus.PreFilteredRejected,
            OpportunityStatus.Rejected,
            OpportunityStatus.DoNotContact
        };
        var deadCategories = new[] { "Non-Urgent Help Request", "Not Relevant" };
        var payIntentSources = new[]
        {
            "remotive", "rss", "rss_jobs", "reddit", "reddit_hiring", "hn_hiring",
            "bounties_opire", "github_bounties", "manual"
        };

        var dead = await db.Opportunities
            .Where(o => deadStatuses.Contains(o.Status) ||
                        (!payIntentSources.Contains(o.SourceKey) &&
                         (deadCategories.Contains(o.ProblemType) ||
                          o.OutreachRecommendation == OutreachRecommendation.Ignore)))
            .ToListAsync(ct);
        if (dead.Count == 0) return;

        await DeleteLeadsKeepDedupAsync(db, dead, ct);
    }

    private static async Task PurgeNonHirableVendorSupportLeadsAsync(DevLeadsDbContext db, CancellationToken ct)
    {
        var active = await db.Opportunities
            .Where(o => o.SourceKey != "manual" && !EngagedStatuses.Contains(o.Status))
            .ToListAsync(ct);
        if (active.Count == 0) return;

        var ids = active.Select(o => o.Id).ToHashSet();
        var bodies = await db.RawSourceItems
            .Where(r => r.OpportunityId != null && ids.Contains(r.OpportunityId.Value))
            .Select(r => new { r.OpportunityId, r.BodyText })
            .ToListAsync(ct);
        var bodyByOpp = bodies
            .GroupBy(r => r.OpportunityId!.Value)
            .ToDictionary(g => g.Key, g => string.Join('\n', g.Select(r => r.BodyText)));

        var dead = active.Where(o =>
        {
            bodyByOpp.TryGetValue(o.Id, out var body);
            var text = $"{o.Title}\n{o.Summary}\n{body}";
            return LeadQualityRules.IsResolvedOrClosedRequest(text) ||
                   LeadQualityRules.IsNonHirableVendorSupportRequest(text) ||
                   LeadQualityRules.IsReplyFeedItem(o.Title) ||
                   LeadQualityRules.IsPromotionalAnnouncement(text);
        }).ToList();
        if (dead.Count == 0) return;

        await DeleteLeadsKeepDedupAsync(db, dead, ct);
    }

    /// <summary>Every visible opportunity must point back to its original public source.</summary>
    private static async Task PurgeSourceLessLeadsAsync(DevLeadsDbContext db, CancellationToken ct)
    {
        var sourceLess = await db.Opportunities
            .Where(o => o.SourceUrl == null || o.SourceUrl == "")
            .ToListAsync(ct);
        if (sourceLess.Count == 0) return;

        await DeleteLeadsKeepDedupAsync(db, sourceLess, ct);
    }

    /// <summary>Removes lead rows while detaching raw items, so dedup never re-ingests the same post.</summary>
    private static async Task DeleteLeadsKeepDedupAsync(DevLeadsDbContext db, List<Opportunity> leads, CancellationToken ct)
    {
        var ids = leads.Select(o => o.Id).ToHashSet();
        var raws = await db.RawSourceItems
            .Where(r => r.OpportunityId != null && ids.Contains(r.OpportunityId.Value))
            .ToListAsync(ct);
        foreach (var r in raws) r.OpportunityId = null;

        db.Opportunities.RemoveRange(leads);
        await db.SaveChangesAsync(ct);
    }
}
