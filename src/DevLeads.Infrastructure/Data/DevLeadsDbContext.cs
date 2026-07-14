using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using DevLeads.Core;
using DevLeads.Core.Entities;

namespace DevLeads.Infrastructure.Data;

/// <summary>EF Core context for the SQLite solo database.</summary>
public class DevLeadsDbContext : DbContext
{
    public DevLeadsDbContext(DbContextOptions<DevLeadsDbContext> options) : base(options) { }

    public DbSet<Opportunity> Opportunities => Set<Opportunity>();
    public DbSet<RawSourceItem> RawSourceItems => Set<RawSourceItem>();
    public DbSet<AiTriageRun> AiTriageRuns => Set<AiTriageRun>();
    public DbSet<OutreachAttempt> OutreachAttempts => Set<OutreachAttempt>();
    public DbSet<Quote> Quotes => Set<Quote>();
    public DbSet<WorkSession> WorkSessions => Set<WorkSession>();
    public DbSet<SuppressionEntry> SuppressionEntries => Set<SuppressionEntry>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();
    public DbSet<SourceConfig> SourceConfigs => Set<SourceConfig>();
    public DbSet<QueryPack> QueryPacks => Set<QueryPack>();
    public DbSet<OperatorSettings> OperatorSettings => Set<OperatorSettings>();
    public DbSet<Skill> Skills => Set<Skill>();
    public DbSet<Campaign> Campaigns => Set<Campaign>();
    public DbSet<TrendSource> TrendSources => Set<TrendSource>();
    public DbSet<TrendSignal> TrendSignals => Set<TrendSignal>();
    public DbSet<ContentTopic> ContentTopics => Set<ContentTopic>();
    public DbSet<ContentDraft> ContentDrafts => Set<ContentDraft>();
    public DbSet<OperatorPost> OperatorPosts => Set<OperatorPost>();
    public DbSet<OperatorPostSnapshot> OperatorPostSnapshots => Set<OperatorPostSnapshot>();
    public DbSet<OperatorMessage> OperatorMessages => Set<OperatorMessage>();
    public DbSet<OperatorPostRevision> OperatorPostRevisions => Set<OperatorPostRevision>();
    public DbSet<Client> Clients => Set<Client>();
    public DbSet<Engagement> Engagements => Set<Engagement>();
    public DbSet<ClientInteraction> ClientInteractions => Set<ClientInteraction>();
    public DbSet<FollowUp> FollowUps => Set<FollowUp>();
    public DbSet<PlatformProfile> PlatformProfiles => Set<PlatformProfile>();
    public DbSet<AdvisorBriefing> AdvisorBriefings => Set<AdvisorBriefing>();
    public DbSet<OperatorDocument> OperatorDocuments => Set<OperatorDocument>();
    public DbSet<EngagementDraft> EngagementDrafts => Set<EngagementDraft>();
    public DbSet<LinkedInProfileField> LinkedInProfileFields => Set<LinkedInProfileField>();

    // SQLite stores DateTimeOffset as TEXT and cannot order/compare it. Convert every
    // DateTimeOffset to sortable UTC ticks (long) so ORDER BY and range filters translate.
    private sealed class DateTimeOffsetToTicksConverter : ValueConverter<DateTimeOffset, long>
    {
        public DateTimeOffsetToTicksConverter()
            : base(v => v.UtcTicks, v => new DateTimeOffset(v, TimeSpan.Zero)) { }
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder b)
    {
        b.Properties<DateTimeOffset>().HaveConversion<DateTimeOffsetToTicksConverter>();

        // Persist all enums as readable strings (matches the TEXT columns in the design schema).
        b.Properties<OpportunityStatus>().HaveConversion<string>();
        b.Properties<Priority>().HaveConversion<string>();
        b.Properties<AiJobStatus>().HaveConversion<string>();
        b.Properties<OutreachRecommendation>().HaveConversion<string>();
        b.Properties<OutreachMode>().HaveConversion<string>();
        b.Properties<OutreachStatus>().HaveConversion<string>();
        b.Properties<OutreachChannel>().HaveConversion<string>();
        b.Properties<QuoteStatus>().HaveConversion<string>();
        b.Properties<WorkSessionStatus>().HaveConversion<string>();
        b.Properties<SuppressionContactType>().HaveConversion<string>();
        b.Properties<ContentTopicStatus>().HaveConversion<string>();
        b.Properties<ContentDraftStatus>().HaveConversion<string>();
        b.Properties<ContentFormat>().HaveConversion<string>();
        b.Properties<OperatorPostStatus>().HaveConversion<string>();
        b.Properties<OperatorMessageKind>().HaveConversion<string>();
        b.Properties<OperatorMessageStatus>().HaveConversion<string>();
        b.Properties<OperatorPostRevisionStatus>().HaveConversion<string>();
        b.Properties<ClientStatus>().HaveConversion<string>();
        b.Properties<EngagementStatus>().HaveConversion<string>();
        b.Properties<FollowUpStatus>().HaveConversion<string>();
        b.Properties<InteractionDirection>().HaveConversion<string>();
        b.Properties<PlatformPresenceStatus>().HaveConversion<string>();
        b.Properties<EngagementDraftKind>().HaveConversion<string>();
        b.Properties<EngagementDraftStatus>().HaveConversion<string>();
    }

    protected override void OnModelCreating(ModelBuilder mb)
    {
        mb.Entity<Opportunity>(e =>
        {
            e.HasIndex(o => o.Status);
            e.HasIndex(o => o.Score);
            e.HasIndex(o => o.SourceKey);
            e.HasIndex(o => o.CampaignId);
            e.HasMany(o => o.TriageRuns).WithOne(t => t.Opportunity!).HasForeignKey(t => t.OpportunityId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(o => o.OutreachAttempts).WithOne(t => t.Opportunity!).HasForeignKey(t => t.OpportunityId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(o => o.Quotes).WithOne(t => t.Opportunity!).HasForeignKey(t => t.OpportunityId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(o => o.WorkSessions).WithOne(t => t.Opportunity!).HasForeignKey(t => t.OpportunityId).OnDelete(DeleteBehavior.Cascade);
        });

        mb.Entity<RawSourceItem>(e =>
        {
            e.HasIndex(r => r.ContentHash);
            e.HasIndex(r => new { r.SourceKey, r.ExternalId }).IsUnique();
        });

        mb.Entity<SourceConfig>().HasIndex(s => s.SourceKey).IsUnique();
        mb.Entity<Campaign>().HasIndex(c => c.Key).IsUnique();
        mb.Entity<TrendSource>().HasIndex(s => s.SeedKey).IsUnique();
        mb.Entity<TrendSignal>(e =>
        {
            e.HasIndex(s => new { s.SourceKey, s.ExternalId }).IsUnique();
            e.HasIndex(s => s.PostedAt);
        });
        mb.Entity<ContentTopic>()
            .HasMany(t => t.Drafts).WithOne(d => d.Topic!)
            .HasForeignKey(d => d.TopicId).OnDelete(DeleteBehavior.Cascade);
        mb.Entity<OperatorPost>(e =>
        {
            e.HasIndex(p => new { p.Platform, p.ExternalId }).IsUnique();
            e.HasMany(p => p.Snapshots).WithOne(s => s.Post!)
                .HasForeignKey(s => s.OperatorPostId).OnDelete(DeleteBehavior.Cascade);
        });
        mb.Entity<OperatorPostRevision>(e =>
        {
            e.HasIndex(r => r.OperatorPostId);
            e.HasIndex(r => r.Status);
            e.HasOne(r => r.Post).WithMany()
                .HasForeignKey(r => r.OperatorPostId).OnDelete(DeleteBehavior.Cascade);
        });
        mb.Entity<OperatorMessage>(e =>
        {
            e.HasIndex(m => new { m.Platform, m.ExternalId }).IsUnique();
            e.HasIndex(m => m.Status);
            e.HasOne(m => m.Post).WithMany()
                .HasForeignKey(m => m.OperatorPostId).OnDelete(DeleteBehavior.SetNull);
        });
        mb.Entity<Client>(e =>
        {
            e.HasIndex(c => c.Status);
            e.HasIndex(c => c.SourceOpportunityId);
            e.HasMany(c => c.Engagements).WithOne(x => x.Client!)
                .HasForeignKey(x => x.ClientId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(c => c.Interactions).WithOne(x => x.Client!)
                .HasForeignKey(x => x.ClientId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(c => c.FollowUps).WithOne(x => x.Client!)
                .HasForeignKey(x => x.ClientId).OnDelete(DeleteBehavior.Cascade);
        });
        mb.Entity<Engagement>().HasIndex(x => x.Status);
        mb.Entity<FollowUp>(e =>
        {
            e.HasIndex(f => f.Status);
            e.HasIndex(f => f.DueAt);
        });
        mb.Entity<PlatformProfile>(e =>
        {
            e.HasIndex(p => p.Key).IsUnique();
            e.HasIndex(p => p.Status);
        });
        mb.Entity<AdvisorBriefing>().HasIndex(b2 => b2.ForDate);
        mb.Entity<OperatorDocument>().HasIndex(d => d.Kind).IsUnique();
        mb.Entity<EngagementDraft>(e =>
        {
            e.HasIndex(d => new { d.Platform, d.ExternalId }).IsUnique();
            e.HasIndex(d => d.Status);
            e.HasOne(d => d.Post).WithMany()
                .HasForeignKey(d => d.OperatorPostId).OnDelete(DeleteBehavior.SetNull);
        });
        mb.Entity<LinkedInProfileField>().HasIndex(f => f.FieldKey).IsUnique();
        mb.Entity<QueryPack>().HasIndex(q => q.Name).IsUnique();
        mb.Entity<SuppressionEntry>().HasIndex(s => s.ContactValue);
        mb.Entity<AuditEvent>().HasIndex(a => new { a.EntityType, a.EntityId });
    }
}
