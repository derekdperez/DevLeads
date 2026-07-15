using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using DevLeads.Core;
using DevLeads.Core.Entities;
using DevLeads.Core.QueryPacks;

namespace DevLeads.Infrastructure.Data;

/// <summary>
/// Creates the database and seeds query packs, source configs, and settings.
/// Also migrates older databases: removes retired sources (GitHub Issues) and purges
/// leads that cannot become paid work or useful owner/operator relationships. Never seeds demo/sample leads.
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
        var campaigns = await SeedCampaignsAsync(db, ct);
        var migrated = await SeedSourceConfigsAsync(db, campaigns, ct);
        await SeedTrendSourcesAsync(db, ct);
        await SeedPlatformProfilesAsync(db, ct);
        await SeedWebScanProbesAsync(db, ct);
        await BackfillLinkedInProfileSnapshotAsync(db, ct);
        await db.SaveChangesAsync(ct);
        await BackfillLeadCampaignsAsync(db, campaigns[EmergencyCampaignKey], ct);

        // One-time when existing sources change: leads gathered under the old,
        // lower-quality configuration are stale — drop everything still in triage
        // stages that the operator never engaged with. Purely additive new sources
        // (e.g. a new campaign's sources) do NOT invalidate existing leads.
        if (retired || replaced || migrated)
            await PurgeStaleDiscoveryLeadsAsync(db, ct);

        await PurgeNonActionableLeadsAsync(db, ct);
        await PurgeNonHirableVendorSupportLeadsAsync(db, ct);
        await PurgeSourceLessLeadsAsync(db, ct);
        await PurgeExpiredDiscoveryLeadsAsync(db, ct);
        await DemoteGenericCapabilitySkillsAsync(db, ct);
        await RequeueTemplateDraftsAsync(db, ct);
    }

    /// <summary>
    /// One-time (2026-07-11): unapproved template mad-lib drafts ("I saw your post about
    /// [title]…") are moved into the AI generation queue so the batched generator rewrites
    /// them grounded in the original posts. Approved/sent drafts are never touched.
    /// </summary>
    private static async Task RequeueTemplateDraftsAsync(DevLeadsDbContext db, CancellationToken ct)
    {
        var templateDrafts = await db.OutreachAttempts
            .Where(a => a.Status == OutreachStatus.PendingApproval && a.TemplateKey == "direct_outreach")
            .ToListAsync(ct);
        if (templateDrafts.Count == 0) return;

        foreach (var a in templateDrafts)
        {
            a.Status = OutreachStatus.QueuedForGeneration;
            a.TemplateKey = "ai_batch_v1";
            a.Body = Services.OutreachService.QueuedPlaceholder;
        }
        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// One-time data fix (2026-07-11): "REST API" was seeded as a weight-3 "Primary stack"
    /// skill, which made every Go/Python job post score as a core .NET fit. Demote it in
    /// place unless the operator already re-weighted it themselves.
    /// </summary>
    private static async Task DemoteGenericCapabilitySkillsAsync(DevLeadsDbContext db, CancellationToken ct)
    {
        var restApi = await db.Skills.FirstOrDefaultAsync(
            s => s.Name == "REST API" && s.Weight == 3 && s.Category == "Primary stack", ct);
        if (restApi is not null)
        {
            restApi.Weight = 2;
            restApi.Category = "Backend";
        }

        // Keep the strongest .NET/Azure specialties grouped consistently in the profile.
        foreach (var (name, oldCategory) in new[] { ("Azure", "Cloud & DevOps"), (".NET modernization", "Specialized") })
        {
            var skill = await db.Skills.FirstOrDefaultAsync(
                s => s.Name == name && s.Category == oldCategory, ct);
            if (skill is not null) skill.Category = "Primary stack";
        }

        // Migrate either prior seeded default to the operator's generalist profile. Custom
        // values remain operator-owned and are never overwritten.
        var settings = await db.OperatorSettings.FirstOrDefaultAsync(ct);
        if (settings?.SecondarySkills is
            "DNS, TLS, hosting, WordPress, WooCommerce, Shopify, Python, Node, PHP" or
            "IIS, Windows Server, DNS, TLS, hosting, SQL performance tuning")
        {
            settings.SecondarySkills =
                "Python, Node.js, React, Angular, PHP, Java, Go, mobile, WordPress, Shopify, Linux, cloud";
        }

        // Real operator identity (2026-07-11) — only replaces the old placeholder defaults.
        if (settings is not null)
        {
            if (settings.OperatorName == "Senior Engineer") settings.OperatorName = "Derek Perez";
            if (settings.Location == "Massachusetts") settings.Location = "Florence, Massachusetts (Western MA)";
        }

        await db.SaveChangesAsync(ct);
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
            "ALTER TABLE Opportunities ADD COLUMN CampaignId INTEGER NULL",
            "ALTER TABLE Opportunities ADD COLUMN LanguageCode TEXT NOT NULL DEFAULT 'en'",
            "ALTER TABLE Opportunities ADD COLUMN TranslatedBody TEXT NOT NULL DEFAULT ''",
            "ALTER TABLE Opportunities ADD COLUMN LanguagePenalty REAL NOT NULL DEFAULT 0",
            "ALTER TABLE SourceConfigs ADD COLUMN CampaignId INTEGER NULL",
            "ALTER TABLE OperatorSettings ADD COLUMN SelectedCampaignId INTEGER NULL",
            "ALTER TABLE OperatorSettings ADD COLUMN ContentDiscoveryEnabled INTEGER NOT NULL DEFAULT 1",
            """
            CREATE TABLE IF NOT EXISTS "TrendSources" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_TrendSources" PRIMARY KEY AUTOINCREMENT,
                "SeedKey" TEXT NOT NULL,
                "Kind" TEXT NOT NULL,
                "DisplayName" TEXT NOT NULL,
                "ParametersJson" TEXT NOT NULL,
                "Enabled" INTEGER NOT NULL,
                "PollIntervalMinutes" INTEGER NOT NULL,
                "MaxItemsPerRun" INTEGER NOT NULL,
                "RequireSkillMatch" INTEGER NOT NULL,
                "LastRunHealthy" INTEGER NOT NULL,
                "LastRunMessage" TEXT NULL,
                "LastRunItemCount" INTEGER NOT NULL,
                "LastRunAt" INTEGER NULL,
                "NextRunAt" INTEGER NULL
            )
            """,
            "CREATE UNIQUE INDEX IF NOT EXISTS \"IX_TrendSources_SeedKey\" ON \"TrendSources\" (\"SeedKey\")",
            """
            CREATE TABLE IF NOT EXISTS "TrendSignals" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_TrendSignals" PRIMARY KEY AUTOINCREMENT,
                "SourceKey" TEXT NOT NULL,
                "ExternalId" TEXT NOT NULL,
                "Url" TEXT NOT NULL,
                "Title" TEXT NOT NULL,
                "Snippet" TEXT NOT NULL,
                "PostedAt" INTEGER NOT NULL,
                "FetchedAt" INTEGER NOT NULL,
                "Engagement" REAL NOT NULL,
                "MatchedSkillsJson" TEXT NOT NULL,
                "Hotness" REAL NOT NULL,
                "UsedInTopic" INTEGER NOT NULL
            )
            """,
            "CREATE UNIQUE INDEX IF NOT EXISTS \"IX_TrendSignals_SourceKey_ExternalId\" ON \"TrendSignals\" (\"SourceKey\", \"ExternalId\")",
            "CREATE INDEX IF NOT EXISTS \"IX_TrendSignals_PostedAt\" ON \"TrendSignals\" (\"PostedAt\")",
            """
            CREATE TABLE IF NOT EXISTS "ContentTopics" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_ContentTopics" PRIMARY KEY AUTOINCREMENT,
                "Title" TEXT NOT NULL,
                "Angle" TEXT NOT NULL,
                "Rationale" TEXT NOT NULL,
                "InterestScore" REAL NOT NULL,
                "SkillsJson" TEXT NOT NULL,
                "EvidenceJson" TEXT NOT NULL,
                "SuggestedFormatsCsv" TEXT NOT NULL,
                "Status" TEXT NOT NULL,
                "CreatedAt" INTEGER NOT NULL,
                "UpdatedAt" INTEGER NOT NULL
            )
            """,
            """
            CREATE TABLE IF NOT EXISTS "ContentDrafts" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_ContentDrafts" PRIMARY KEY AUTOINCREMENT,
                "TopicId" INTEGER NOT NULL,
                "Format" TEXT NOT NULL,
                "Title" TEXT NOT NULL,
                "BodyMarkdown" TEXT NOT NULL,
                "WordCount" INTEGER NOT NULL,
                "Status" TEXT NOT NULL,
                "Provider" TEXT NOT NULL,
                "Model" TEXT NOT NULL,
                "CreatedAt" INTEGER NOT NULL,
                "UpdatedAt" INTEGER NOT NULL,
                CONSTRAINT "FK_ContentDrafts_ContentTopics_TopicId" FOREIGN KEY ("TopicId") REFERENCES "ContentTopics" ("Id") ON DELETE CASCADE
            )
            """,
            "CREATE INDEX IF NOT EXISTS \"IX_ContentDrafts_TopicId\" ON \"ContentDrafts\" (\"TopicId\")",
            "ALTER TABLE OperatorSettings ADD COLUMN RedditUsername TEXT NOT NULL DEFAULT 'Mission_Turn3102'",
            "ALTER TABLE OperatorSettings ADD COLUMN ContactEmail TEXT NOT NULL DEFAULT 'derekdperez@gmail.com'",
            """
            CREATE TABLE IF NOT EXISTS "OperatorPosts" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_OperatorPosts" PRIMARY KEY AUTOINCREMENT,
                "Platform" TEXT NOT NULL,
                "ExternalId" TEXT NOT NULL,
                "Url" TEXT NOT NULL,
                "Title" TEXT NOT NULL,
                "Body" TEXT NOT NULL,
                "Community" TEXT NOT NULL,
                "Status" TEXT NOT NULL,
                "CampaignId" INTEGER NULL,
                "ReplyCount" INTEGER NOT NULL,
                "LastCheckedAt" INTEGER NULL,
                "Notes" TEXT NOT NULL,
                "PostedAt" INTEGER NOT NULL,
                "CreatedAt" INTEGER NOT NULL,
                "UpdatedAt" INTEGER NOT NULL
            )
            """,
            "CREATE UNIQUE INDEX IF NOT EXISTS \"IX_OperatorPosts_Platform_ExternalId\" ON \"OperatorPosts\" (\"Platform\", \"ExternalId\")",
            "ALTER TABLE OperatorPosts ADD COLUMN ViewCount INTEGER NOT NULL DEFAULT 0",
            "ALTER TABLE OperatorPosts ADD COLUMN ViewCountKnown INTEGER NOT NULL DEFAULT 0",
            "ALTER TABLE OperatorPosts ADD COLUMN ThreadSummary TEXT NOT NULL DEFAULT ''",
            "ALTER TABLE OperatorPosts ADD COLUMN SummarizedAt INTEGER NULL",
            "ALTER TABLE OperatorPosts ADD COLUMN UpvoteCount INTEGER NOT NULL DEFAULT 0",
            "ALTER TABLE OperatorPostSnapshots ADD COLUMN UpvoteCount INTEGER NOT NULL DEFAULT 0",
            "ALTER TABLE OperatorPostSnapshots ADD COLUMN ViewCount INTEGER NOT NULL DEFAULT 0",
            "ALTER TABLE OperatorSettings ADD COLUMN RedditClientId TEXT NOT NULL DEFAULT ''",
            "ALTER TABLE OperatorSettings ADD COLUMN RedditClientSecret TEXT NOT NULL DEFAULT ''",
            "ALTER TABLE OperatorSettings ADD COLUMN RedditAppPassword TEXT NOT NULL DEFAULT ''",
            """
            CREATE TABLE IF NOT EXISTS "OperatorPostSnapshots" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_OperatorPostSnapshots" PRIMARY KEY AUTOINCREMENT,
                "OperatorPostId" INTEGER NOT NULL,
                "At" INTEGER NOT NULL,
                "ReplyCount" INTEGER NOT NULL,
                CONSTRAINT "FK_OperatorPostSnapshots_OperatorPosts_OperatorPostId" FOREIGN KEY ("OperatorPostId") REFERENCES "OperatorPosts" ("Id") ON DELETE CASCADE
            )
            """,
            "CREATE INDEX IF NOT EXISTS \"IX_OperatorPostSnapshots_OperatorPostId\" ON \"OperatorPostSnapshots\" (\"OperatorPostId\")",
            "ALTER TABLE OperatorSettings ADD COLUMN RedditInboxFeedToken TEXT NOT NULL DEFAULT '7ff9c88db61f73b0ddc5162ab49eb0acf3f79f42'",
            // Codex (OpenAI) CLI provider + per-feature provider/model overrides. Empty
            // override = inherit the global AiProvider/AiModel; post optimization defaults
            // to Codex/gpt-5.6-sol (a stronger, stable writer for the rewrite experiment).
            "ALTER TABLE OperatorSettings ADD COLUMN CodexCliPath TEXT NOT NULL DEFAULT 'codex'",
            "ALTER TABLE OperatorSettings ADD COLUMN TriageAiProvider TEXT NOT NULL DEFAULT ''",
            "ALTER TABLE OperatorSettings ADD COLUMN TriageAiModel TEXT NOT NULL DEFAULT ''",
            "ALTER TABLE OperatorSettings ADD COLUMN OutreachAiProvider TEXT NOT NULL DEFAULT ''",
            "ALTER TABLE OperatorSettings ADD COLUMN OutreachAiModel TEXT NOT NULL DEFAULT ''",
            "ALTER TABLE OperatorSettings ADD COLUMN ContentTopicsAiProvider TEXT NOT NULL DEFAULT ''",
            "ALTER TABLE OperatorSettings ADD COLUMN ContentTopicsAiModel TEXT NOT NULL DEFAULT ''",
            "ALTER TABLE OperatorSettings ADD COLUMN ContentDraftsAiProvider TEXT NOT NULL DEFAULT ''",
            "ALTER TABLE OperatorSettings ADD COLUMN ContentDraftsAiModel TEXT NOT NULL DEFAULT ''",
            "ALTER TABLE OperatorSettings ADD COLUMN PostDraftAiProvider TEXT NOT NULL DEFAULT ''",
            "ALTER TABLE OperatorSettings ADD COLUMN PostDraftAiModel TEXT NOT NULL DEFAULT ''",
            "ALTER TABLE OperatorSettings ADD COLUMN ThreadSummaryAiProvider TEXT NOT NULL DEFAULT ''",
            "ALTER TABLE OperatorSettings ADD COLUMN ThreadSummaryAiModel TEXT NOT NULL DEFAULT ''",
            "ALTER TABLE OperatorSettings ADD COLUMN PostOptimizationAiProvider TEXT NOT NULL DEFAULT 'Codex'",
            "ALTER TABLE OperatorSettings ADD COLUMN PostOptimizationAiModel TEXT NOT NULL DEFAULT 'gpt-5.6-sol'",
            """
            CREATE TABLE IF NOT EXISTS "OperatorMessages" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_OperatorMessages" PRIMARY KEY AUTOINCREMENT,
                "Platform" TEXT NOT NULL,
                "ExternalId" TEXT NOT NULL,
                "Kind" TEXT NOT NULL,
                "Author" TEXT NOT NULL,
                "Subject" TEXT NOT NULL,
                "Body" TEXT NOT NULL,
                "Community" TEXT NOT NULL,
                "Url" TEXT NOT NULL,
                "Status" TEXT NOT NULL,
                "OperatorPostId" INTEGER NULL,
                "Notes" TEXT NOT NULL,
                "ReceivedAt" INTEGER NOT NULL,
                "CreatedAt" INTEGER NOT NULL,
                "UpdatedAt" INTEGER NOT NULL,
                CONSTRAINT "FK_OperatorMessages_OperatorPosts_OperatorPostId" FOREIGN KEY ("OperatorPostId") REFERENCES "OperatorPosts" ("Id") ON DELETE SET NULL
            )
            """,
            "CREATE UNIQUE INDEX IF NOT EXISTS \"IX_OperatorMessages_Platform_ExternalId\" ON \"OperatorMessages\" (\"Platform\", \"ExternalId\")",
            "CREATE INDEX IF NOT EXISTS \"IX_OperatorMessages_Status\" ON \"OperatorMessages\" (\"Status\")",
            "CREATE INDEX IF NOT EXISTS \"IX_OperatorMessages_OperatorPostId\" ON \"OperatorMessages\" (\"OperatorPostId\")",
            """
            CREATE TABLE IF NOT EXISTS "OperatorPostRevisions" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_OperatorPostRevisions" PRIMARY KEY AUTOINCREMENT,
                "OperatorPostId" INTEGER NOT NULL,
                "Approach" TEXT NOT NULL,
                "Rationale" TEXT NOT NULL,
                "OldTitle" TEXT NOT NULL,
                "OldBody" TEXT NOT NULL,
                "NewTitle" TEXT NOT NULL,
                "NewBody" TEXT NOT NULL,
                "Status" TEXT NOT NULL,
                "Provider" TEXT NOT NULL,
                "Model" TEXT NOT NULL,
                "BaselineViewCount" INTEGER NOT NULL,
                "BaselineReplyCount" INTEGER NOT NULL,
                "BaselineUpvoteCount" INTEGER NOT NULL,
                "BaselineViewsPerDay" REAL NOT NULL,
                "BaselineRepliesPerDay" REAL NOT NULL,
                "ResultPostId" INTEGER NULL,
                "Notes" TEXT NOT NULL,
                "CreatedAt" INTEGER NOT NULL,
                "AppliedAt" INTEGER NULL,
                CONSTRAINT "FK_OperatorPostRevisions_OperatorPosts_OperatorPostId" FOREIGN KEY ("OperatorPostId") REFERENCES "OperatorPosts" ("Id") ON DELETE CASCADE
            )
            """,
            "CREATE INDEX IF NOT EXISTS \"IX_OperatorPostRevisions_OperatorPostId\" ON \"OperatorPostRevisions\" (\"OperatorPostId\")",
            "CREATE INDEX IF NOT EXISTS \"IX_OperatorPostRevisions_Status\" ON \"OperatorPostRevisions\" (\"Status\")",
            """
            CREATE TABLE IF NOT EXISTS "Campaigns" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_Campaigns" PRIMARY KEY AUTOINCREMENT,
                "Key" TEXT NOT NULL,
                "Name" TEXT NOT NULL,
                "Emoji" TEXT NOT NULL,
                "Objective" TEXT NOT NULL,
                "Enabled" INTEGER NOT NULL,
                "CreatedAt" INTEGER NOT NULL
            )
            """,
            "CREATE UNIQUE INDEX IF NOT EXISTS \"IX_Campaigns_Key\" ON \"Campaigns\" (\"Key\")",
            "CREATE INDEX IF NOT EXISTS \"IX_Opportunities_CampaignId\" ON \"Opportunities\" (\"CampaignId\")",
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
            """,
            // Clients & engagements + Today advisor + platform presence (2026-07-13).
            "ALTER TABLE OperatorSettings ADD COLUMN AdvisorAiProvider TEXT NOT NULL DEFAULT ''",
            "ALTER TABLE OperatorSettings ADD COLUMN AdvisorAiModel TEXT NOT NULL DEFAULT ''",
            "ALTER TABLE OperatorSettings ADD COLUMN PlatformDiscoveryAiProvider TEXT NOT NULL DEFAULT ''",
            "ALTER TABLE OperatorSettings ADD COLUMN PlatformDiscoveryAiModel TEXT NOT NULL DEFAULT ''",
            """
            CREATE TABLE IF NOT EXISTS "Clients" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_Clients" PRIMARY KEY AUTOINCREMENT,
                "Name" TEXT NOT NULL,
                "Company" TEXT NOT NULL,
                "Platform" TEXT NOT NULL,
                "Handle" TEXT NOT NULL,
                "Email" TEXT NOT NULL,
                "ProfileUrl" TEXT NOT NULL,
                "Status" TEXT NOT NULL,
                "SourceOpportunityId" INTEGER NULL,
                "CampaignId" INTEGER NULL,
                "Notes" TEXT NOT NULL,
                "CreatedAt" INTEGER NOT NULL,
                "UpdatedAt" INTEGER NOT NULL
            )
            """,
            "CREATE INDEX IF NOT EXISTS \"IX_Clients_Status\" ON \"Clients\" (\"Status\")",
            "CREATE INDEX IF NOT EXISTS \"IX_Clients_SourceOpportunityId\" ON \"Clients\" (\"SourceOpportunityId\")",
            """
            CREATE TABLE IF NOT EXISTS "Engagements" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_Engagements" PRIMARY KEY AUTOINCREMENT,
                "ClientId" INTEGER NOT NULL,
                "Title" TEXT NOT NULL,
                "Description" TEXT NOT NULL,
                "Status" TEXT NOT NULL,
                "AgreedFee" REAL NULL,
                "OpportunityId" INTEGER NULL,
                "StartedAt" INTEGER NULL,
                "DueAt" INTEGER NULL,
                "ClosedAt" INTEGER NULL,
                "NextDeliverable" TEXT NOT NULL,
                "Notes" TEXT NOT NULL,
                "CreatedAt" INTEGER NOT NULL,
                "UpdatedAt" INTEGER NOT NULL,
                CONSTRAINT "FK_Engagements_Clients_ClientId" FOREIGN KEY ("ClientId") REFERENCES "Clients" ("Id") ON DELETE CASCADE
            )
            """,
            "CREATE INDEX IF NOT EXISTS \"IX_Engagements_ClientId\" ON \"Engagements\" (\"ClientId\")",
            "CREATE INDEX IF NOT EXISTS \"IX_Engagements_Status\" ON \"Engagements\" (\"Status\")",
            """
            CREATE TABLE IF NOT EXISTS "ClientInteractions" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_ClientInteractions" PRIMARY KEY AUTOINCREMENT,
                "ClientId" INTEGER NOT NULL,
                "OccurredAt" INTEGER NOT NULL,
                "Channel" TEXT NOT NULL,
                "Direction" TEXT NOT NULL,
                "Summary" TEXT NOT NULL,
                "Url" TEXT NOT NULL,
                "CreatedAt" INTEGER NOT NULL,
                CONSTRAINT "FK_ClientInteractions_Clients_ClientId" FOREIGN KEY ("ClientId") REFERENCES "Clients" ("Id") ON DELETE CASCADE
            )
            """,
            "CREATE INDEX IF NOT EXISTS \"IX_ClientInteractions_ClientId\" ON \"ClientInteractions\" (\"ClientId\")",
            """
            CREATE TABLE IF NOT EXISTS "FollowUps" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_FollowUps" PRIMARY KEY AUTOINCREMENT,
                "ClientId" INTEGER NOT NULL,
                "EngagementId" INTEGER NULL,
                "Note" TEXT NOT NULL,
                "DueAt" INTEGER NOT NULL,
                "Status" TEXT NOT NULL,
                "CompletedAt" INTEGER NULL,
                "CreatedAt" INTEGER NOT NULL,
                CONSTRAINT "FK_FollowUps_Clients_ClientId" FOREIGN KEY ("ClientId") REFERENCES "Clients" ("Id") ON DELETE CASCADE
            )
            """,
            "CREATE INDEX IF NOT EXISTS \"IX_FollowUps_ClientId\" ON \"FollowUps\" (\"ClientId\")",
            "CREATE INDEX IF NOT EXISTS \"IX_FollowUps_Status\" ON \"FollowUps\" (\"Status\")",
            "CREATE INDEX IF NOT EXISTS \"IX_FollowUps_DueAt\" ON \"FollowUps\" (\"DueAt\")",
            """
            CREATE TABLE IF NOT EXISTS "PlatformProfiles" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_PlatformProfiles" PRIMARY KEY AUTOINCREMENT,
                "Key" TEXT NOT NULL,
                "Name" TEXT NOT NULL,
                "Url" TEXT NOT NULL,
                "SignupUrl" TEXT NOT NULL,
                "Category" TEXT NOT NULL,
                "Audience" TEXT NOT NULL,
                "Rationale" TEXT NOT NULL,
                "PostingNotes" TEXT NOT NULL,
                "CostModel" TEXT NOT NULL,
                "Source" TEXT NOT NULL,
                "Status" TEXT NOT NULL,
                "Handle" TEXT NOT NULL,
                "ProfileUrl" TEXT NOT NULL,
                "GeneratedBio" TEXT NOT NULL,
                "Notes" TEXT NOT NULL,
                "ActivatedAt" INTEGER NULL,
                "CreatedAt" INTEGER NOT NULL,
                "UpdatedAt" INTEGER NOT NULL
            )
            """,
            "CREATE UNIQUE INDEX IF NOT EXISTS \"IX_PlatformProfiles_Key\" ON \"PlatformProfiles\" (\"Key\")",
            "CREATE INDEX IF NOT EXISTS \"IX_PlatformProfiles_Status\" ON \"PlatformProfiles\" (\"Status\")",
            """
            CREATE TABLE IF NOT EXISTS "AdvisorBriefings" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_AdvisorBriefings" PRIMARY KEY AUTOINCREMENT,
                "ForDate" INTEGER NOT NULL,
                "BodyMarkdown" TEXT NOT NULL,
                "Provider" TEXT NOT NULL,
                "Model" TEXT NOT NULL,
                "CreatedAt" INTEGER NOT NULL
            )
            """,
            "CREATE INDEX IF NOT EXISTS \"IX_AdvisorBriefings_ForDate\" ON \"AdvisorBriefings\" (\"ForDate\")",
            "ALTER TABLE PlatformProfiles ADD COLUMN SignupPackJson TEXT NOT NULL DEFAULT ''",
            "ALTER TABLE PlatformProfiles ADD COLUMN RequiresResume INTEGER NOT NULL DEFAULT 0",
            """
            CREATE TABLE IF NOT EXISTS "OperatorDocuments" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_OperatorDocuments" PRIMARY KEY AUTOINCREMENT,
                "Kind" TEXT NOT NULL,
                "FileName" TEXT NOT NULL,
                "ContentType" TEXT NOT NULL,
                "SizeBytes" INTEGER NOT NULL,
                "Data" BLOB NOT NULL,
                "UploadedAt" INTEGER NOT NULL
            )
            """,
            "CREATE UNIQUE INDEX IF NOT EXISTS \"IX_OperatorDocuments_Kind\" ON \"OperatorDocuments\" (\"Kind\")",
            // LinkedIn profile management, OAuth, scheduling, and reviewed engagement drafts.
            "ALTER TABLE OperatorPosts ADD COLUMN ScheduledAt INTEGER NULL",
            "ALTER TABLE OperatorSettings ADD COLUMN LinkedInEngagementAiProvider TEXT NOT NULL DEFAULT ''",
            "ALTER TABLE OperatorSettings ADD COLUMN LinkedInEngagementAiModel TEXT NOT NULL DEFAULT ''",
            "ALTER TABLE OperatorSettings ADD COLUMN LinkedInClientId TEXT NOT NULL DEFAULT ''",
            "ALTER TABLE OperatorSettings ADD COLUMN LinkedInClientSecret TEXT NOT NULL DEFAULT ''",
            "ALTER TABLE OperatorSettings ADD COLUMN LinkedInRedirectUri TEXT NOT NULL DEFAULT ''",
            "ALTER TABLE OperatorSettings ADD COLUMN LinkedInScopes TEXT NOT NULL DEFAULT 'openid profile email w_member_social'",
            "ALTER TABLE OperatorSettings ADD COLUMN LinkedInApiVersion TEXT NOT NULL DEFAULT '202606'",
            "ALTER TABLE OperatorSettings ADD COLUMN LinkedInAccessToken TEXT NOT NULL DEFAULT ''",
            "ALTER TABLE OperatorSettings ADD COLUMN LinkedInAccessTokenExpiresAt INTEGER NULL",
            "ALTER TABLE OperatorSettings ADD COLUMN LinkedInRefreshToken TEXT NOT NULL DEFAULT ''",
            "ALTER TABLE OperatorSettings ADD COLUMN LinkedInRefreshTokenExpiresAt INTEGER NULL",
            "ALTER TABLE OperatorSettings ADD COLUMN LinkedInMemberId TEXT NOT NULL DEFAULT ''",
            "ALTER TABLE OperatorSettings ADD COLUMN LinkedInMemberName TEXT NOT NULL DEFAULT ''",
            "ALTER TABLE OperatorSettings ADD COLUMN LinkedInMemberPictureUrl TEXT NOT NULL DEFAULT ''",
            "ALTER TABLE OperatorSettings ADD COLUMN LinkedInOAuthState TEXT NOT NULL DEFAULT ''",
            "ALTER TABLE OperatorSettings ADD COLUMN LinkedInOAuthStateExpiresAt INTEGER NULL",
            """
            CREATE TABLE IF NOT EXISTS "EngagementDrafts" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_EngagementDrafts" PRIMARY KEY AUTOINCREMENT,
                "Platform" TEXT NOT NULL,
                "Kind" TEXT NOT NULL,
                "ExternalId" TEXT NOT NULL,
                "OperatorPostId" INTEGER NULL,
                "ThreadUrn" TEXT NOT NULL,
                "ParentCommentUrn" TEXT NOT NULL,
                "AuthorUrn" TEXT NOT NULL,
                "AuthorName" TEXT NOT NULL,
                "SourceText" TEXT NOT NULL,
                "SourceUrl" TEXT NOT NULL,
                "DraftText" TEXT NOT NULL,
                "Status" TEXT NOT NULL,
                "Provider" TEXT NOT NULL,
                "Model" TEXT NOT NULL,
                "LastError" TEXT NOT NULL,
                "ReceivedAt" INTEGER NOT NULL,
                "CreatedAt" INTEGER NOT NULL,
                "UpdatedAt" INTEGER NOT NULL,
                "PublishedAt" INTEGER NULL,
                CONSTRAINT "FK_EngagementDrafts_OperatorPosts_OperatorPostId" FOREIGN KEY ("OperatorPostId") REFERENCES "OperatorPosts" ("Id") ON DELETE SET NULL
            )
            """,
            "CREATE UNIQUE INDEX IF NOT EXISTS \"IX_EngagementDrafts_Platform_ExternalId\" ON \"EngagementDrafts\" (\"Platform\", \"ExternalId\")",
            "CREATE INDEX IF NOT EXISTS \"IX_EngagementDrafts_Status\" ON \"EngagementDrafts\" (\"Status\")",
            "CREATE INDEX IF NOT EXISTS \"IX_EngagementDrafts_OperatorPostId\" ON \"EngagementDrafts\" (\"OperatorPostId\")",
            // LinkedIn profile studio (2026-07-14): locally tracked profile sections with
            // per-field AI rewrite proposals, plus the overall AI review on settings.
            "ALTER TABLE OperatorSettings ADD COLUMN LinkedInProfileAiProvider TEXT NOT NULL DEFAULT 'Codex'",
            "ALTER TABLE OperatorSettings ADD COLUMN LinkedInProfileAiModel TEXT NOT NULL DEFAULT 'gpt-5.6-sol'",
            "ALTER TABLE OperatorSettings ADD COLUMN LinkedInProfileReview TEXT NOT NULL DEFAULT ''",
            "ALTER TABLE OperatorSettings ADD COLUMN LinkedInProfileReviewAt INTEGER NULL",
            """
            CREATE TABLE IF NOT EXISTS "LinkedInProfileFields" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_LinkedInProfileFields" PRIMARY KEY AUTOINCREMENT,
                "FieldKey" TEXT NOT NULL,
                "DisplayName" TEXT NOT NULL,
                "Guidance" TEXT NOT NULL,
                "SortOrder" INTEGER NOT NULL,
                "CurrentText" TEXT NOT NULL,
                "SuggestedText" TEXT NOT NULL,
                "SuggestedAt" INTEGER NULL,
                "Provider" TEXT NOT NULL,
                "Model" TEXT NOT NULL,
                "UpdatedAt" INTEGER NOT NULL
            )
            """,
            "CREATE UNIQUE INDEX IF NOT EXISTS \"IX_LinkedInProfileFields_FieldKey\" ON \"LinkedInProfileFields\" (\"FieldKey\")",
            // LinkedIn next-actions plan (2026-07-14): per-section profile editing is
            // retired in favor of one pasted whole-profile snapshot plus an AI-planned,
            // operator-executed action checklist.
            "ALTER TABLE OperatorSettings ADD COLUMN LinkedInProfileSnapshot TEXT NOT NULL DEFAULT ''",
            """
            CREATE TABLE IF NOT EXISTS "LinkedInActions" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_LinkedInActions" PRIMARY KEY AUTOINCREMENT,
                "Category" TEXT NOT NULL,
                "Title" TEXT NOT NULL,
                "Why" TEXT NOT NULL,
                "Steps" TEXT NOT NULL,
                "Status" TEXT NOT NULL,
                "SortOrder" INTEGER NOT NULL,
                "Provider" TEXT NOT NULL,
                "Model" TEXT NOT NULL,
                "GeneratedAt" INTEGER NOT NULL,
                "CompletedAt" INTEGER NULL,
                "UpdatedAt" INTEGER NOT NULL
            )
            """,
            "CREATE INDEX IF NOT EXISTS \"IX_LinkedInActions_Status\" ON \"LinkedInActions\" (\"Status\")",
            "CREATE INDEX IF NOT EXISTS \"IX_LinkedInActions_Category\" ON \"LinkedInActions\" (\"Category\")",
            // Site rescue (2026-07-14): active passive-scan for broken business web assets that
            // could become paid repair work, plus a per-feature AI override and swappable
            // discovery search endpoint.
            "ALTER TABLE OperatorSettings ADD COLUMN WebAssetOutreachAiProvider TEXT NOT NULL DEFAULT ''",
            "ALTER TABLE OperatorSettings ADD COLUMN WebAssetOutreachAiModel TEXT NOT NULL DEFAULT ''",
            // ExecuteSqlRawAsync runs the SQL through string.Format, so literal braces must be
            // doubled — {{q}} reaches SQLite as {q}. A bare {q} throws FormatException at boot.
            "ALTER TABLE OperatorSettings ADD COLUMN WebScanSearchEndpoint TEXT NOT NULL DEFAULT 'https://html.duckduckgo.com/html/?q={{q}}'",
            "ALTER TABLE OperatorSettings ADD COLUMN WebScanMaxTargetsPerRun INTEGER NOT NULL DEFAULT 40",
            """
            CREATE TABLE IF NOT EXISTS "WebScanProbes" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_WebScanProbes" PRIMARY KEY AUTOINCREMENT,
                "Name" TEXT NOT NULL,
                "Description" TEXT NOT NULL,
                "SoftwarePackage" TEXT NOT NULL,
                "ErrorSignatures" TEXT NOT NULL,
                "PathsToCheck" TEXT NOT NULL,
                "DiscoveryQueries" TEXT NOT NULL,
                "FlagServerErrors" INTEGER NOT NULL,
                "Enabled" INTEGER NOT NULL,
                "CreatedAt" INTEGER NOT NULL,
                "UpdatedAt" INTEGER NOT NULL,
                "LastRunAt" INTEGER NULL,
                "LastRunChecked" INTEGER NOT NULL,
                "LastRunFound" INTEGER NOT NULL
            )
            """,
            "CREATE UNIQUE INDEX IF NOT EXISTS \"IX_WebScanProbes_Name\" ON \"WebScanProbes\" (\"Name\")",
            """
            CREATE TABLE IF NOT EXISTS "WebAssetFindings" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_WebAssetFindings" PRIMARY KEY AUTOINCREMENT,
                "ProbeId" INTEGER NULL,
                "ProbeName" TEXT NOT NULL,
                "Url" TEXT NOT NULL,
                "Host" TEXT NOT NULL,
                "BusinessName" TEXT NOT NULL,
                "Severity" TEXT NOT NULL,
                "Detection" TEXT NOT NULL,
                "Status" TEXT NOT NULL,
                "HttpStatus" INTEGER NOT NULL,
                "Signal" TEXT NOT NULL,
                "Evidence" TEXT NOT NULL,
                "DetectedSoftware" TEXT NOT NULL,
                "ContactEmail" TEXT NOT NULL,
                "ContactSource" TEXT NOT NULL,
                "OutreachSubject" TEXT NOT NULL,
                "OutreachBody" TEXT NOT NULL,
                "OutreachProvider" TEXT NOT NULL,
                "OutreachModel" TEXT NOT NULL,
                "OutreachGeneratedAt" INTEGER NULL,
                "Notes" TEXT NOT NULL,
                "FirstSeenAt" INTEGER NOT NULL,
                "LastCheckedAt" INTEGER NOT NULL,
                "CreatedAt" INTEGER NOT NULL,
                "UpdatedAt" INTEGER NOT NULL
            )
            """,
            "CREATE UNIQUE INDEX IF NOT EXISTS \"IX_WebAssetFindings_Url\" ON \"WebAssetFindings\" (\"Url\")",
            "CREATE INDEX IF NOT EXISTS \"IX_WebAssetFindings_Status\" ON \"WebAssetFindings\" (\"Status\")",
            "CREATE INDEX IF NOT EXISTS \"IX_WebAssetFindings_Host\" ON \"WebAssetFindings\" (\"Host\")",
            // Discord bot integration (2026-07-14): post ads via the operator's own bot,
            // monitor invited channels for replies/mentions, reviewed reply drafts.
            "ALTER TABLE OperatorSettings ADD COLUMN DiscordBotToken TEXT NOT NULL DEFAULT ''",
            "ALTER TABLE OperatorSettings ADD COLUMN DiscordApplicationId TEXT NOT NULL DEFAULT ''",
            "ALTER TABLE OperatorSettings ADD COLUMN DiscordBotUserId TEXT NOT NULL DEFAULT ''",
            "ALTER TABLE OperatorSettings ADD COLUMN DiscordBotName TEXT NOT NULL DEFAULT ''",
            "ALTER TABLE OperatorSettings ADD COLUMN DiscordUserId TEXT NOT NULL DEFAULT ''",
            "ALTER TABLE OperatorSettings ADD COLUMN DiscordEngagementAiProvider TEXT NOT NULL DEFAULT ''",
            "ALTER TABLE OperatorSettings ADD COLUMN DiscordEngagementAiModel TEXT NOT NULL DEFAULT ''",
            "ALTER TABLE OperatorPosts ADD COLUMN DiscordChannelId TEXT NOT NULL DEFAULT ''",
            """
            CREATE TABLE IF NOT EXISTS "DiscordChannels" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_DiscordChannels" PRIMARY KEY AUTOINCREMENT,
                "GuildId" TEXT NOT NULL,
                "GuildName" TEXT NOT NULL,
                "ChannelId" TEXT NOT NULL,
                "ChannelName" TEXT NOT NULL,
                "MonitorEnabled" INTEGER NOT NULL,
                "LastSyncedMessageId" TEXT NOT NULL,
                "LastSyncedAt" INTEGER NULL,
                "Notes" TEXT NOT NULL,
                "Stale" INTEGER NOT NULL,
                "CreatedAt" INTEGER NOT NULL,
                "UpdatedAt" INTEGER NOT NULL
            )
            """,
            "CREATE UNIQUE INDEX IF NOT EXISTS \"IX_DiscordChannels_ChannelId\" ON \"DiscordChannels\" (\"ChannelId\")"
        };
        foreach (var sql in upgrades)
        {
            try { await db.Database.ExecuteSqlRawAsync(sql, ct); }
            catch (Microsoft.Data.Sqlite.SqliteException) { /* column already exists */ }
        }
    }

    /// <summary>
    /// Seeds a couple of ready-to-run Site rescue probes so the scanner works out of the box.
    /// Only seeds when the table is empty — the operator owns probes after that.
    /// </summary>
    private static async Task SeedWebScanProbesAsync(DevLeadsDbContext db, CancellationToken ct)
    {
        if (await db.WebScanProbes.AnyAsync(ct)) return;
        var now = DateTimeOffset.UtcNow;
        db.WebScanProbes.AddRange(
            new WebScanProbe
            {
                Name = "WordPress database & PHP errors",
                Description = "Small-business WordPress sites showing a public database or PHP fatal error — a fast, well-paid fix.",
                SoftwarePackage = "WordPress",
                ErrorSignatures = string.Join('\n',
                    "Error establishing a database connection",
                    "There has been a critical error on this website",
                    "Fatal error:",
                    "Warning: mysqli"),
                PathsToCheck = "/",
                DiscoveryQueries = string.Join('\n',
                    "\"Error establishing a database connection\"",
                    "\"There has been a critical error on this website\""),
                FlagServerErrors = true,
                Enabled = true,
                CreatedAt = now,
                UpdatedAt = now
            },
            new WebScanProbe
            {
                Name = "Generic 5xx outage",
                Description = "Any business site currently returning a 5xx / gateway error or a visible server-error page.",
                SoftwarePackage = "",
                ErrorSignatures = string.Join('\n',
                    "500 Internal Server Error",
                    "502 Bad Gateway",
                    "503 Service Unavailable",
                    "Service Temporarily Unavailable"),
                PathsToCheck = "/",
                DiscoveryQueries = "",
                FlagServerErrors = true,
                Enabled = true,
                CreatedAt = now,
                UpdatedAt = now
            });
        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// One-time move from the retired per-section profile studio to the single pasted
    /// snapshot the action plan reviews: whatever the operator pasted per section becomes
    /// the initial snapshot text. The LinkedInProfileFields rows are kept as history.
    /// </summary>
    private static async Task BackfillLinkedInProfileSnapshotAsync(DevLeadsDbContext db, CancellationToken ct)
    {
        var settings = await db.OperatorSettings.FirstOrDefaultAsync(ct);
        if (settings is null || !string.IsNullOrWhiteSpace(settings.LinkedInProfileSnapshot)) return;
        var fields = await db.LinkedInProfileFields.AsNoTracking()
            .Where(f => f.CurrentText != "").OrderBy(f => f.SortOrder).ToListAsync(ct);
        if (fields.Count == 0) return;
        settings.LinkedInProfileSnapshot = string.Join("\n\n",
            fields.Select(f => f.DisplayName + ":\n" + f.CurrentText.Trim()));
        await db.SaveChangesAsync(ct);
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

    public const string EmergencyCampaignKey = "emergency";
    public const string ModernizationCampaignKey = "dotnet_modernization";
    public const string AiAutomationCampaignKey = "ai_automation";
    public const string MentoringCampaignKey = "mentoring";

    /// <summary>
    /// Ensures the built-in campaigns exist (add-only: name/objective edits belong to the
    /// operator afterwards). Returns campaign ids keyed by stable campaign key.
    /// </summary>
    private static async Task<Dictionary<string, long>> SeedCampaignsAsync(DevLeadsDbContext db, CancellationToken ct)
    {
        var seeds = new[]
        {
            new Campaign
            {
                Key = EmergencyCampaignKey,
                Name = "Emergency rescue",
                Emoji = "🚨",
                Objective =
                    "Urgent, paid emergency software repair work: production outages, broken sites and " +
                    "checkouts, failed deployments, database incidents, DNS/TLS breakage. A qualifying lead " +
                    "is a business owner, operator, or agency describing an active problem they would hire " +
                    "and pay someone to fix right now.",
                Enabled = true,
                CreatedAt = DateTimeOffset.UtcNow
            },
            new Campaign
            {
                Key = ModernizationCampaignKey,
                Name = ".NET modernization consulting",
                Emoji = "🏗️",
                Objective =
                    "Consulting engagements to modernize legacy .NET enterprise applications: migrating " +
                    ".NET Framework / WebForms / WCF / WinForms / VB.NET / Classic ASP systems to modern " +
                    ".NET, replatforming on-prem apps to Azure, upgrading SQL Server, or incrementally " +
                    "rewriting a legacy codebase. A qualifying lead is a company, CTO, or team seeking " +
                    "outside help (hire/pay intent) with a planned modernization or migration — these are " +
                    "multi-week projects, not emergencies.",
                Enabled = true,
                CreatedAt = DateTimeOffset.UtcNow
            },
            new Campaign
            {
                Key = AiAutomationCampaignKey,
                Name = "AI & automation projects",
                Emoji = "🤖",
                Objective =
                    "Paid consulting and project work to design, build, integrate, or repair practical " +
                    "AI and business automation: LLM/RAG systems, chatbots and agents, document/data " +
                    "workflows, API integrations, n8n, Zapier, Make, and internal tools. A qualifying " +
                    "lead is an owner, company, or team explicitly seeking outside help or offering a " +
                    "budget. Reject AI news, product promotion, tutorials, hobby or academic projects, " +
                    "and unpaid open-source or volunteer requests.",
                Enabled = true,
                CreatedAt = DateTimeOffset.UtcNow
            },
            new Campaign
            {
                Key = MentoringCampaignKey,
                Name = "Paid mentoring calls",
                Emoji = "🎓",
                Objective =
                    "Paid 1:1 voice or video calls with a 20-year .NET veteran: career advice, " +
                    "programming and architecture questions, code and debugging help, or anything else " +
                    "his experience covers. $20–40 per hour depending on topic and length, booked by " +
                    "direct message, remote worldwide. A qualifying lead is an individual developer, " +
                    "student, or career-changer explicitly willing to pay for direct help or mentorship.",
                Enabled = true,
                CreatedAt = DateTimeOffset.UtcNow
            }
        };

        var added = false;
        foreach (var seed in seeds)
        {
            if (!await db.Campaigns.AnyAsync(c => c.Key == seed.Key, ct))
            {
                db.Campaigns.Add(seed);
                added = true;
            }
        }
        if (added) await db.SaveChangesAsync(ct);

        return await db.Campaigns.ToDictionaryAsync(c => c.Key, c => c.Id, ct);
    }

    private static async Task<bool> SeedSourceConfigsAsync(
        DevLeadsDbContext db, Dictionary<string, long> campaigns, CancellationToken ct)
    {
        var emergencyId = campaigns[EmergencyCampaignKey];
        var modernizationId = campaigns[ModernizationCampaignKey];
        var aiAutomationId = campaigns[AiAutomationCampaignKey];
        var migrated = false;
        foreach (var seed in DefaultSources(emergencyId, modernizationId, aiAutomationId))
        {
            var existing = await db.SourceConfigs.FirstOrDefaultAsync(s => s.SourceKey == seed.SourceKey, ct);
            if (existing is null)
            {
                // Purely additive: a new source never invalidates leads gathered by others.
                db.SourceConfigs.Add(seed);
                continue;
            }

            // Campaign assignment is a backfill, not a migration — reassigning it must not
            // purge leads, and an operator's explicit reassignment is never overwritten.
            existing.CampaignId ??= seed.CampaignId;

            if (IsLegacyDefaultSource(existing))
            {
                // Adding communities to the hiring scan broadens coverage; it does not make
                // leads gathered under the previous list stale. Preserve all current triage
                // rows instead of invoking the destructive source-migration cleanup.
                var additiveHiringExpansion = IsAdditiveHiringSubredditExpansion(existing, seed);
                var initialAiTopicGate = IsInitialAiTopicGate(existing);
                var aiTopicGateBroadening = IsAiTopicGateBroadening(existing);
                var aiThresholdRecalibration = IsAiThresholdRecalibration(existing);
                var technologyAgnosticBroadening = IsTechnologyAgnosticSourceBroadening(existing);
                var changed = ApplySourceDefaults(existing, seed);
                migrated |= changed && !additiveHiringExpansion && !initialAiTopicGate &&
                            !aiTopicGateBroadening && !aiThresholdRecalibration &&
                            !technologyAgnosticBroadening;
            }
        }

        // Sources the operator created by hand belong to the emergency campaign by default.
        foreach (var orphan in await db.SourceConfigs.Where(s => s.CampaignId == null).ToListAsync(ct))
            orphan.CampaignId = emergencyId;

        return migrated;
    }

    /// <summary>Assigns campaign-less leads to their source's campaign (manual/unknown → emergency).</summary>
    private static async Task BackfillLeadCampaignsAsync(DevLeadsDbContext db, long emergencyId, CancellationToken ct)
    {
        if (!await db.Opportunities.AnyAsync(o => o.CampaignId == null, ct)) return;

        var campaignBySource = await db.SourceConfigs
            .Where(s => s.CampaignId != null)
            .ToDictionaryAsync(s => s.SourceKey, s => s.CampaignId!.Value, ct);

        foreach (var opp in await db.Opportunities.Where(o => o.CampaignId == null).ToListAsync(ct))
            opp.CampaignId = campaignBySource.TryGetValue(opp.SourceKey, out var cid) ? cid : emergencyId;
        await db.SaveChangesAsync(ct);
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
            or "bounties_opire" or "github_bounties" or "github_feature_requests" ||
        IsInitialAiTopicGate(source) ||
        IsAiTopicGateBroadening(source) ||
        IsAiThresholdRecalibration(source);

    private static bool IsInitialAiTopicGate(SourceConfig source) =>
        IsAiAutomationSource(source) &&
        !source.ParametersJson.Contains("\"requiredQueryPack\"", StringComparison.Ordinal);

    /// <summary>
    /// 2026-07-13: the AI campaign's topic gate moved from the hire-shaped
    /// AiAutomationProjects phrases (which rejected 98% of fetched items, including paid
    /// listings) to the broad whole-word AiAutomationTopic vocabulary. Broadening a gate
    /// only ADMITS more items, so nothing previously gathered goes stale — never purge.
    /// </summary>
    private static bool IsAiTopicGateBroadening(SourceConfig source) =>
        IsAiAutomationSource(source) &&
        source.ParametersJson.Contains("\"requiredQueryPack\":\"AiAutomationProjects\"", StringComparison.Ordinal);

    /// <summary>
    /// 2026-07-13: the AI campaign's MinOpportunityScore dropped from 42–48 (emergency
    /// calibration) to 36–40. AI/automation leads earn no urgency or .NET stack-fit
    /// points, so real paid bounties scored 26–42 and every one died at the threshold;
    /// 36 still sits above the 35 claimed-work competition cap. Loosening a threshold
    /// only admits more items — nothing gathered before goes stale, never purge.
    /// </summary>
    private static bool IsAiThresholdRecalibration(SourceConfig source) =>
        IsAiAutomationSource(source) && source.MinOpportunityScore >= 42;

    /// <summary>
    /// 2026-07-13: bounty and paid-feature sources stopped requiring a .NET-profile text
    /// match. This only broadens discovery, so existing leads remain valid.
    /// </summary>
    private static bool IsTechnologyAgnosticSourceBroadening(SourceConfig source)
    {
        if (source.SourceKey == "bounties_opire")
        {
            // The old built-in omitted the flag, and Opire historically interpreted an
            // omitted value as true.
            return !source.ParametersJson.Contains(
                "\"requireSkillMatch\":\"false\"", StringComparison.OrdinalIgnoreCase);
        }

        return source.SourceKey is "github_bounties" or "github_feature_requests" &&
               (source.ParametersJson.Contains("requireSkillMatch", StringComparison.OrdinalIgnoreCase) ||
                source.ParametersJson.Contains("language:C#", StringComparison.OrdinalIgnoreCase) ||
                source.ParametersJson.Contains("language:TypeScript", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsAiAutomationSource(SourceConfig source) =>
        source.SourceKey is "ai_remotive" or "ai_rss" or "ai_reddit" or "ai_hackernews"
            or "ai_stackexchange" or "ai_github_paid" or "ai_opire";

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

    private static bool IsAdditiveHiringSubredditExpansion(SourceConfig target, SourceConfig seed)
    {
        if (target.SourceKey != "reddit_hiring" ||
            target.DisplayName != seed.DisplayName || target.Enabled != seed.Enabled ||
            target.PollIntervalMinutes != seed.PollIntervalMinutes ||
            target.MaxItemsPerRun != seed.MaxItemsPerRun ||
            target.QueryPacksCsv != seed.QueryPacksCsv ||
            target.MinPreFilterScore != seed.MinPreFilterScore ||
            target.MinOpportunityScore != seed.MinOpportunityScore ||
            target.DraftThreshold != seed.DraftThreshold ||
            target.AlertThreshold != seed.AlertThreshold ||
            target.AutoModeEligible != seed.AutoModeEligible)
            return false;

        try
        {
            using var oldDoc = JsonDocument.Parse(target.ParametersJson);
            using var newDoc = JsonDocument.Parse(seed.ParametersJson);
            var oldRoot = oldDoc.RootElement;
            var newRoot = newDoc.RootElement;
            if (oldRoot.EnumerateObject().Any(p => p.Name != "connector" && p.Name != "subreddits" &&
                                                   p.Name != "daysBack" && p.Name != "requireHiring") ||
                newRoot.EnumerateObject().Any(p => p.Name != "connector" && p.Name != "subreddits" &&
                                                   p.Name != "daysBack" && p.Name != "requireHiring"))
                return false;

            static string Text(JsonElement root, string name) =>
                root.TryGetProperty(name, out var value) ? value.ToString() : "";
            if (Text(oldRoot, "connector") != Text(newRoot, "connector") ||
                Text(oldRoot, "daysBack") != Text(newRoot, "daysBack") ||
                Text(oldRoot, "requireHiring") != Text(newRoot, "requireHiring"))
                return false;

            var oldSubs = Text(oldRoot, "subreddits").Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var newSubs = Text(newRoot, "subreddits").Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            return oldSubs.Count > 0 && oldSubs.IsProperSubsetOf(newSubs);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static IEnumerable<SourceConfig> DefaultSources(
        long emergencyCampaignId, long modernizationCampaignId, long aiAutomationCampaignId)
    {
        foreach (var source in EmergencySources())
        {
            source.CampaignId = emergencyCampaignId;
            yield return source;
        }
        foreach (var source in ModernizationSources())
        {
            source.CampaignId = modernizationCampaignId;
            yield return source;
        }
        foreach (var source in AiAutomationSources())
        {
            source.CampaignId = aiAutomationCampaignId;
            yield return source;
        }
    }

    /// <summary>
    /// Default sources are chosen for commercial intent: places where a business owner,
    /// manager, or agency is describing a problem they are prepared to pay to solve.
    /// </summary>
    private static IEnumerable<SourceConfig> EmergencySources() => new[]
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
            ParametersJson = "{\"connector\":\"reddit\",\"subreddits\":\"forhire;hiring;jobbit;hireawebdeveloper;hireaprogrammer;freelance_forhire;devopsjobs;sysadminjobs;dotnetjobs;programmingjobs;remotejobs;jobs;slavelabour\",\"daysBack\":\"10\",\"requireHiring\":\"true\"}" },

        // Bounty platforms: money is already attached to the work. Connector-side
        // skill filtering keeps only bounties that touch the operator's profile.
        new SourceConfig { SourceKey = "bounties_opire", DisplayName = "Opire — open bounties on GitHub issues", Enabled = true,
            PollIntervalMinutes = 240, MaxItemsPerRun = 40,
            QueryPacksCsv = "HireIntent,PaidFeatureRequest,ContractProjectWork",
            AutoModeEligible = false, MinPreFilterScore = 1, MinOpportunityScore = 40,
            ParametersJson = "{\"connector\":\"opire\",\"maxPages\":\"4\",\"minAmountUsd\":\"20\",\"requireSkillMatch\":\"false\"}" },

        // GitHub bounty ecosystem: BountyHub, Algora, IssueHunt etc. all anchor bounties
        // to public GitHub issues — one search net catches them all, skill-filtered.
        new SourceConfig { SourceKey = "github_bounties", DisplayName = "GitHub bounties — BountyHub / Algora / IssueHunt", Enabled = true,
            PollIntervalMinutes = 120, MaxItemsPerRun = 60,
            QueryPacksCsv = "HireIntent,PaidFeatureRequest,ContractProjectWork",
            AutoModeEligible = false, MinPreFilterScore = 1, MinOpportunityScore = 40,
            // '*' skips the old per-item stack filter. All technologies are eligible;
            // the downstream pay, freshness, competition, and trust gates decide fit.
            ParametersJson = JsonSerializer.Serialize(new
            {
                connector = "github_search",
                daysBack = LeadQualityRules.MaxAutomatedLeadAgeDays.ToString(), requireSkillMatch = "false",
                queries = string.Join('\n',
                    "*label:bounty",
                    "*label:\"💎 Bounty\"",              // Algora's standard bounty label
                    "*bountyhub.dev in:body,comments",     // BountyHub (~all volume, it's small)
                    "*label:\"💵 Funded on Issuehunt\"",
                    "*label:reward",
                    "*bounty in:title")
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
                daysBack = LeadQualityRules.MaxAutomatedLeadAgeDays.ToString(), requireSkillMatch = "false",
                queries = string.Join('\n',
                    "*\"willing to pay\"",
                    "*\"would pay for\"",
                    "*\"happy to sponsor\"",
                    "*\"willing to fund\"",
                    "*\"paid feature request\"",
                    "*bounty in:title")
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

    /// <summary>
    /// Sources for the .NET legacy modernization consulting campaign. Feeds/queries are
    /// chosen to be disjoint from the emergency sources where possible; when a post
    /// qualifies for both campaigns, whichever source fetches it first keeps the lead.
    /// All feed URLs verified live 2026-07-10.
    /// </summary>
    private static IEnumerable<SourceConfig> ModernizationSources() => new[]
    {
        // .NET / Azure job boards: companies paying for modernization-adjacent work.
        new SourceConfig { SourceKey = "mod_rss_jobs", DisplayName = ".NET modernization — job feeds", Enabled = true,
            PollIntervalMinutes = 60, MaxItemsPerRun = 120,
            QueryPacksCsv = "DotNetModernization,ContractProjectWork,HireIntent",
            MinPreFilterScore = 1, MinOpportunityScore = 42,
            ParametersJson = RssParams("21", new[] {
                "https://remoteok.com/remote-dot-net-jobs.rss",
                "https://remotefirstjobs.com/rss/jobs/azure.rss",
                "https://jobicy.com/jobs/feed?industry=software-engineering" }) },

        // HN stories mentioning legacy .NET / modernization pain — searched via Algolia
        // with this campaign's own terms, so the result set differs from the emergency
        // "hackernews" source even though the connector is shared.
        new SourceConfig { SourceKey = "mod_hackernews", DisplayName = ".NET modernization — HN radar", Enabled = true,
            PollIntervalMinutes = 60, MaxItemsPerRun = 50,
            QueryPacksCsv = "DotNetModernization,HireIntent",
            AutoModeEligible = false, MinPreFilterScore = 1, MinOpportunityScore = 42,
            ParametersJson = "{\"connector\":\"hackernews\",\"daysBack\":\"30\"}" },

        // .NET communities: teams describing legacy estates, migration plans, and hiring
        // needs. Overlaps r/dotnet (emergency webdev source) — pack-scoped pre-filtering
        // means each campaign only claims posts matching its own vocabulary.
        new SourceConfig { SourceKey = "mod_reddit", DisplayName = ".NET modernization — Reddit communities", Enabled = true,
            PollIntervalMinutes = 45, MaxItemsPerRun = 120,
            QueryPacksCsv = "DotNetModernization,ContractProjectWork,HireIntent",
            AutoModeEligible = false, MinPreFilterScore = 5, MinOpportunityScore = 44,
            ParametersJson = "{\"connector\":\"reddit\",\"subreddits\":\"dotnet;csharp;dotnetcore;azure;sqlserver;softwarearchitecture\",\"daysBack\":\"14\",\"requireHiring\":\"false\"}" },

        // Disabled by default: the anonymous Stack Exchange quota (~300 req/day/IP) is
        // already consumed by the emergency radar. Enable only after trading polling
        // budget away from stackexchange_radar.
        new SourceConfig { SourceKey = "mod_stackexchange", DisplayName = ".NET modernization — Stack Exchange radar", Enabled = false,
            PollIntervalMinutes = 360, MaxItemsPerRun = 60,
            QueryPacksCsv = "DotNetModernization,ContractProjectWork",
            AutoModeEligible = false, MinPreFilterScore = 6, MinOpportunityScore = 46,
            ParametersJson = "{\"connector\":\"stackexchange\",\"sites\":\"stackoverflow;softwareengineering;dba\",\"daysBack\":\"7\"}" },
    };

    /// <summary>
    /// Searches every registered connector for paid AI/automation implementation work.
    /// The broad AiAutomationTopic pack gates campaign relevance (whole-word matched, so
    /// "gpt"/"llm"/"automation" mentions keep a post in the arena) and supplies short
    /// connector search terms; the hire-shaped AiAutomationProjects pack plus pay/urgency
    /// signals still decide what scores, so general AI discussion never becomes a lead.
    /// </summary>
    private static IEnumerable<SourceConfig> AiAutomationSources() => new[]
    {
        new SourceConfig { SourceKey = "ai_remotive", DisplayName = "AI automation — Remotive jobs", Enabled = true,
            PollIntervalMinutes = 120, MaxItemsPerRun = 80,
            QueryPacksCsv = "AiAutomationProjects,AiAutomationTopic,ContractProjectWork,HireIntent",
            MinPreFilterScore = 20, MinOpportunityScore = 36,
            ParametersJson = "{\"connector\":\"remotive\",\"category\":\"software-dev\",\"jobTypes\":\"any\",\"requiredQueryPack\":\"AiAutomationTopic\"}" },

        new SourceConfig { SourceKey = "ai_rss", DisplayName = "AI automation — jobs + project forums", Enabled = true,
            PollIntervalMinutes = 90, MaxItemsPerRun = 140,
            QueryPacksCsv = "AiAutomationProjects,AiAutomationTopic,ContractProjectWork,HireIntent,PaidFeatureRequest",
            AutoModeEligible = false, MinPreFilterScore = 20, MinOpportunityScore = 36,
            ParametersJson = RssParams("21", new[] {
                "https://weworkremotely.com/categories/remote-programming-jobs.rss",
                "https://remoteok.com/remote-ai-jobs.rss",
                "https://remoteok.com/remote-machine-learning-jobs.rss",
                "https://jobicy.com/jobs/feed?industry=software-engineering&type=contract",
                "https://community.openai.com/latest.rss",
                "https://community.n8n.io/latest.rss",
                "https://community.make.com/latest.rss",
                "https://community.retool.com/latest.rss",
                "https://forum.bubble.io/latest.rss" }, requiredQueryPack: "AiAutomationTopic") },

        new SourceConfig { SourceKey = "ai_reddit", DisplayName = "AI automation — Reddit projects + hiring", Enabled = true,
            PollIntervalMinutes = 45, MaxItemsPerRun = 140,
            QueryPacksCsv = "AiAutomationProjects,AiAutomationTopic,ContractProjectWork,HireIntent,PaidFeatureRequest",
            AutoModeEligible = false, MinPreFilterScore = 20, MinOpportunityScore = 36,
            ParametersJson = "{\"connector\":\"reddit\",\"subreddits\":\"artificial;MachineLearning;OpenAI;LocalLLaMA;automation;n8n;zapier;SaaS;smallbusiness;Entrepreneur;forhire;freelance_forhire;programmingjobs;dotnetjobs\",\"daysBack\":\"14\",\"requireHiring\":\"false\",\"requiredQueryPack\":\"AiAutomationTopic\"}" },

        new SourceConfig { SourceKey = "ai_hackernews", DisplayName = "AI automation — Hacker News", Enabled = true,
            PollIntervalMinutes = 90, MaxItemsPerRun = 60,
            QueryPacksCsv = "AiAutomationProjects,AiAutomationTopic,ContractProjectWork,HireIntent",
            AutoModeEligible = false, MinPreFilterScore = 20, MinOpportunityScore = 36,
            ParametersJson = "{\"connector\":\"hackernews\",\"daysBack\":\"30\",\"requiredQueryPack\":\"AiAutomationTopic\"}" },

        // Low polling frequency protects the shared anonymous Stack Exchange daily quota.
        new SourceConfig { SourceKey = "ai_stackexchange", DisplayName = "AI automation — Stack Exchange radar", Enabled = true,
            PollIntervalMinutes = 1440, MaxItemsPerRun = 50,
            QueryPacksCsv = "AiAutomationProjects,AiAutomationTopic,ContractProjectWork,HireIntent",
            AutoModeEligible = false, MinPreFilterScore = 24, MinOpportunityScore = 40,
            ParametersJson = "{\"connector\":\"stackexchange\",\"sites\":\"stackoverflow;ai;datascience;softwareengineering\",\"daysBack\":\"7\",\"requiredQueryPack\":\"AiAutomationTopic\"}" },

        new SourceConfig { SourceKey = "ai_github_paid", DisplayName = "AI automation — paid GitHub issues", Enabled = true,
            PollIntervalMinutes = 240, MaxItemsPerRun = 60,
            QueryPacksCsv = "AiAutomationProjects,AiAutomationTopic,HireIntent,PaidFeatureRequest",
            AutoModeEligible = false, MinPreFilterScore = 20, MinOpportunityScore = 36,
            ParametersJson = JsonSerializer.Serialize(new
            {
                connector = "github_search", daysBack = "120", requireSkillMatch = "false",
                requiredQueryPack = "AiAutomationTopic",
                queries = string.Join('\n',
                    "*label:bounty \"LLM\"", "*label:bounty \"RAG\"", "*label:bounty \"chatbot\"",
                    "*label:bounty \"OpenAI\"", "*\"willing to pay\" \"AI integration\"",
                    "*\"willing to pay\" \"workflow automation\"")
            }) },

        new SourceConfig { SourceKey = "ai_opire", DisplayName = "AI automation — Opire bounties", Enabled = true,
            PollIntervalMinutes = 360, MaxItemsPerRun = 80,
            QueryPacksCsv = "AiAutomationProjects,AiAutomationTopic,HireIntent,PaidFeatureRequest",
            AutoModeEligible = false, MinPreFilterScore = 20, MinOpportunityScore = 36,
            ParametersJson = "{\"connector\":\"opire\",\"maxPages\":\"8\",\"minAmountUsd\":\"20\",\"requireSkillMatch\":\"false\",\"requiredQueryPack\":\"AiAutomationTopic\"}" },
    };

    /// <summary>
    /// Content-studio trend sources (add-only; the operator owns them afterwards). Feed
    /// URLs verified live 2026-07-11. These fetch *signals* for topic generation, never
    /// leads — see TrendScanService.
    /// </summary>
    private static async Task SeedTrendSourcesAsync(DevLeadsDbContext db, CancellationToken ct)
    {
        var seeds = new[]
        {
            // Releases of the operator's core stack: every entry is on-topic by construction.
            new TrendSource { SeedKey = "trend_dotnet_releases", Kind = "rss", DisplayName = "Trends — .NET release feeds",
                RequireSkillMatch = false, PollIntervalMinutes = 720, MaxItemsPerRun = 40,
                ParametersJson = RssParams("30", new[] {
                    "https://github.com/dotnet/runtime/releases.atom",
                    "https://github.com/dotnet/aspnetcore/releases.atom",
                    "https://github.com/dotnet/efcore/releases.atom" }) },

            new TrendSource { SeedKey = "trend_vendor_blogs", Kind = "rss", DisplayName = "Trends — vendor engineering blogs",
                RequireSkillMatch = false, PollIntervalMinutes = 720, MaxItemsPerRun = 40,
                ParametersJson = RssParams("21", new[] {
                    "https://devblogs.microsoft.com/dotnet/feed/",
                    "https://devblogs.microsoft.com/azure-sql/feed/",
                    "https://code.visualstudio.com/feed.xml",
                    "https://blog.jetbrains.com/dotnet/feed/" }) },

            // Secondary-stack ecosystems: skill-filtered so release noise stays out.
            new TrendSource { SeedKey = "trend_ecosystem_news", Kind = "rss", DisplayName = "Trends — ecosystem news (WordPress, Node)",
                RequireSkillMatch = true, PollIntervalMinutes = 720, MaxItemsPerRun = 40,
                ParametersJson = RssParams("14", new[] {
                    "https://wordpress.org/news/feed/",
                    "https://nodejs.org/en/feed/blog.xml" }) },

            // What practitioners are talking about right now; engagement (points) feeds hotness.
            new TrendSource { SeedKey = "trend_hackernews", Kind = "hackernews", DisplayName = "Trends — Hacker News (skill terms)",
                RequireSkillMatch = true, PollIntervalMinutes = 360, MaxItemsPerRun = 60,
                ParametersJson = "{\"daysBack\":\"7\"}" },

            // One multireddit request per cycle — top.rss is empty for anonymous clients,
            // so this reads new.rss and lets skill-match + recency do the ranking.
            new TrendSource { SeedKey = "trend_reddit", Kind = "rss", DisplayName = "Trends — developer subreddits",
                RequireSkillMatch = true, PollIntervalMinutes = 720, MaxItemsPerRun = 50,
                ParametersJson = RssParams("7", new[] {
                    "https://www.reddit.com/r/dotnet+csharp+programming+webdev+sysadmin+azure/new.rss?limit=50" }) },
        };

        foreach (var seed in seeds)
            if (!await db.TrendSources.AnyAsync(s => s.SeedKey == seed.SeedKey, ct))
                db.TrendSources.Add(seed);
    }

    /// <summary>
    /// Platform-presence catalog (add-only by Key; the operator owns rows afterwards).
    /// A catalog entry whose key already has tracked operator posts starts Active —
    /// the account demonstrably exists — everything else starts as a suggestion.
    /// </summary>
    private static async Task SeedPlatformProfilesAsync(DevLeadsDbContext db, CancellationToken ct)
    {
        var existingKeys = (await db.PlatformProfiles.Select(p => p.Key).ToListAsync(ct))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var postPlatforms = (await db.OperatorPosts.Select(p => p.Platform).Distinct().ToListAsync(ct))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var now = DateTimeOffset.UtcNow;

        foreach (var seed in Core.Platforms.DefaultPlatformCatalog.All)
        {
            if (existingKeys.Contains(seed.Key)) continue;
            var active = postPlatforms.Contains(seed.Key);
            db.PlatformProfiles.Add(new PlatformProfile
            {
                Key = seed.Key,
                Name = seed.Name,
                Url = seed.Url,
                SignupUrl = seed.SignupUrl,
                Category = seed.Category,
                Audience = seed.Audience,
                Rationale = seed.Rationale,
                PostingNotes = seed.PostingNotes,
                CostModel = seed.CostModel,
                RequiresResume = seed.RequiresResume,
                Source = "seed",
                Status = active ? PlatformPresenceStatus.Active : PlatformPresenceStatus.Suggested,
                ActivatedAt = active ? now : null,
                CreatedAt = now, UpdatedAt = now
            });
        }

        // One-time backfill after the RequiresResume column lands: rows seeded before the
        // flag existed pick up the catalog's value. Guarded so an operator who later
        // unchecks a platform isn't fought every boot.
        if (!await db.PlatformProfiles.AnyAsync(p => p.RequiresResume, ct))
        {
            var resumeKeys = Core.Platforms.DefaultPlatformCatalog.All
                .Where(s => s.RequiresResume).Select(s => s.Key).ToList();
            foreach (var row in await db.PlatformProfiles
                         .Where(p => resumeKeys.Contains(p.Key)).ToListAsync(ct))
                row.RequiresResume = true;
        }
    }

    // Single overload on purpose: a (daysBack, params feeds) / (daysBack, provider, params feeds)
    // pair is ambiguous — C# bound the first FEED as the provider, silently dropping the feed
    // and disabling AI triage for the source.
    private static string RssParams(string daysBack, string[] feeds, string? triageProvider = null,
        string? requiredQueryPack = null)
    {
        var p = new Dictionary<string, object> { ["connector"] = "rss", ["daysBack"] = daysBack, ["feeds"] = feeds };
        if (triageProvider is not null) p["triageProvider"] = triageProvider;
        if (requiredQueryPack is not null) p["requiredQueryPack"] = requiredQueryPack;
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

    /// <summary>Statuses meaning the operator has actually engaged with the lead — never auto-purge these.
    /// Archived counts as engaged: the operator explicitly dismissed it, and keeping the row
    /// (with its raw item) is what prevents the same post from ever being re-ingested.</summary>
    private static readonly OpportunityStatus[] EngagedStatuses =
    {
        OpportunityStatus.Approved, OpportunityStatus.Contacted, OpportunityStatus.Responded,
        OpportunityStatus.Qualified, OpportunityStatus.QuoteDrafted, OpportunityStatus.QuoteSent,
        OpportunityStatus.Accepted, OpportunityStatus.InProgress, OpportunityStatus.Fixed,
        OpportunityStatus.PaymentPending, OpportunityStatus.Paid, OpportunityStatus.Won,
        OpportunityStatus.Lost, OpportunityStatus.Archived
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
    /// Removes untouched automated leads older than the standard discovery window while
    /// preserving raw dedup evidence, manual entries, and anything the operator engaged.
    /// </summary>
    private static async Task PurgeExpiredDiscoveryLeadsAsync(DevLeadsDbContext db, CancellationToken ct)
    {
        var cutoff = DateTimeOffset.UtcNow.AddDays(-LeadQualityRules.MaxAutomatedLeadAgeDays);
        var expired = await db.Opportunities
            .Where(o => o.SourceKey != "manual" && o.PostedAt < cutoff &&
                        !EngagedStatuses.Contains(o.Status))
            .ToListAsync(ct);
        if (expired.Count == 0) return;

        await DeleteLeadsKeepDedupAsync(db, expired, ct);
    }

    /// <summary>
    /// Purges leads that will not lead to paid work or a useful owner/operator relationship:
    /// pre-filter rejects, triage rejects, do-not-contact posts, and irrelevant help requests.
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
            "bounties_opire", "github_bounties", "manual", "mod_rss_jobs"
        };

        var dead = await db.Opportunities
            .Where(o => deadStatuses.Contains(o.Status) ||
                        (o.Status != OpportunityStatus.JustMissed &&
                         !payIntentSources.Contains(o.SourceKey) &&
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
