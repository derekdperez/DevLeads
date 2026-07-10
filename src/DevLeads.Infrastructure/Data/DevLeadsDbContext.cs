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
    }

    protected override void OnModelCreating(ModelBuilder mb)
    {
        mb.Entity<Opportunity>(e =>
        {
            e.HasIndex(o => o.Status);
            e.HasIndex(o => o.Score);
            e.HasIndex(o => o.SourceKey);
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
        mb.Entity<QueryPack>().HasIndex(q => q.Name).IsUnique();
        mb.Entity<SuppressionEntry>().HasIndex(s => s.ContactValue);
        mb.Entity<AuditEvent>().HasIndex(a => new { a.EntityType, a.EntityId });
    }
}
