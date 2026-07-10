using Microsoft.EntityFrameworkCore;
using DevLeads.Core;
using DevLeads.Core.Entities;
using DevLeads.Infrastructure.Data;

namespace DevLeads.Infrastructure.Services;

/// <summary>Periodic housekeeping: stale-lead archiving and overdue-quote flagging.</summary>
public sealed class MaintenanceService
{
    private readonly DevLeadsDbContext _db;
    private readonly AuditService _audit;

    public MaintenanceService(DevLeadsDbContext db, AuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    public async Task<int> ArchiveStaleLeadsAsync(CancellationToken ct)
    {
        var settings = await _db.OperatorSettings.FirstOrDefaultAsync(ct) ?? new Core.Entities.OperatorSettings();
        var cutoff = DateTimeOffset.UtcNow.AddHours(-settings.StaleItemMaxAgeHours);

        var stale = await _db.Opportunities
            .Where(o => (o.Status == OpportunityStatus.New || o.Status == OpportunityStatus.NeedsReview
                        || o.Status == OpportunityStatus.AiTriaged || o.Status == OpportunityStatus.FollowUpLater)
                        && o.Score < 40 && o.PostedAt < cutoff)
            .ToListAsync(ct);

        foreach (var o in stale)
        {
            o.Status = OpportunityStatus.Rejected;
            o.RejectionReason = "Archived: stale low-value lead past age cutoff.";
            o.UpdatedAt = DateTimeOffset.UtcNow;
        }
        if (stale.Count > 0)
        {
            _audit.Record("Maintenance", 0, "ArchiveStale", $"Archived {stale.Count} stale lead(s).");
            await _db.SaveChangesAsync(ct);
        }
        return stale.Count;
    }

    public async Task<int> RejectNonHirableVendorSupportAsync(CancellationToken ct)
    {
        var active = await _db.Opportunities
            .Where(o => o.Status == OpportunityStatus.New ||
                        o.Status == OpportunityStatus.NeedsReview ||
                        o.Status == OpportunityStatus.AiTriaged ||
                        o.Status == OpportunityStatus.DraftReady ||
                        o.Status == OpportunityStatus.FollowUpLater)
            .ToListAsync(ct);
        if (active.Count == 0) return 0;

        var ids = active.Select(o => o.Id).ToHashSet();
        var bodies = await _db.RawSourceItems
            .Where(r => r.OpportunityId != null && ids.Contains(r.OpportunityId.Value))
            .Select(r => new { r.OpportunityId, r.BodyText })
            .ToListAsync(ct);
        var bodyByOpp = bodies
            .GroupBy(r => r.OpportunityId!.Value)
            .ToDictionary(g => g.Key, g => string.Join('\n', g.Select(r => r.BodyText)));

        var rejected = new List<Opportunity>();
        foreach (var o in active)
        {
            bodyByOpp.TryGetValue(o.Id, out var body);
            var text = $"{o.Title}\n{o.Summary}\n{body}";
            var resolved = LeadQualityRules.IsResolvedOrClosedRequest(text);
            if (!resolved && !LeadQualityRules.IsNonHirableVendorSupportRequest(text))
                continue;

            o.Status = OpportunityStatus.Rejected;
            o.OutreachRecommendation = OutreachRecommendation.Ignore;
            o.RejectionReason = resolved
                ? "Thread is already solved or has an accepted answer."
                : "Vendor account/billing support request, not hirable third-party repair work.";
            o.UpdatedAt = DateTimeOffset.UtcNow;
            rejected.Add(o);
        }

        if (rejected.Count > 0)
        {
            _audit.Record("Maintenance", 0, "RejectVendorSupport", $"Rejected {rejected.Count} vendor support lead(s).");
            await _db.SaveChangesAsync(ct);
        }
        return rejected.Count;
    }

    public async Task<int> FlagOverdueQuotesAsync(CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var sent = await _db.Quotes.Where(q => q.Status == QuoteStatus.Sent && q.DueAt != null).ToListAsync(ct);
        var overdue = sent.Where(q => q.DueAt < now).ToList();
        foreach (var q in overdue) q.Status = QuoteStatus.Overdue;
        if (overdue.Count > 0) await _db.SaveChangesAsync(ct);
        return overdue.Count;
    }

    /// <summary>Count of opportunities whose follow-up is now due (surfaced on the dashboard).</summary>
    public async Task<int> DueFollowUpCountAsync(CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var dates = await _db.Opportunities
            .Where(o => o.NextFollowUpAt != null && o.Status != OpportunityStatus.Won && o.Status != OpportunityStatus.Lost)
            .Select(o => o.NextFollowUpAt).ToListAsync(ct);
        return dates.Count(d => d <= now);
    }
}
