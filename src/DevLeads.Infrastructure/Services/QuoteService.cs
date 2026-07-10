using Microsoft.EntityFrameworkCore;
using DevLeads.Core;
using DevLeads.Core.Entities;
using DevLeads.Core.Templates;
using DevLeads.Infrastructure.Data;

namespace DevLeads.Infrastructure.Services;

/// <summary>Quote generation and payment-state tracking for bounded emergency fixes.</summary>
public sealed class QuoteService
{
    private readonly DevLeadsDbContext _db;
    private readonly AuditService _audit;

    public QuoteService(DevLeadsDbContext db, AuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    public async Task<Quote> GenerateAsync(long opportunityId, double? amount, bool dueOnCompletion, CancellationToken ct)
    {
        var opp = await _db.Opportunities.FirstOrDefaultAsync(o => o.Id == opportunityId, ct)
                  ?? throw new InvalidOperationException("Opportunity not found");

        var (min, max) = PricingTiers.SuggestFor(opp.ProblemType);
        var fee = amount ?? Math.Round((min + max) / 2, 0);

        var quote = new Quote
        {
            OpportunityId = opportunityId,
            Amount = fee,
            PaymentDueUponCompletion = dueOnCompletion,
            Scope =
                "- Diagnose the current production failure\n" +
                "- Identify the immediate cause\n" +
                "- Apply a fix or safe rollback if available\n" +
                "- Confirm the system is working again",
            Exclusions = "Unrelated feature work, major rewrites, or long-term infrastructure changes unless separately agreed.",
            Status = QuoteStatus.Drafted,
            CreatedAt = DateTimeOffset.UtcNow
        };
        _db.Quotes.Add(quote);
        opp.Status = OpportunityStatus.QuoteDrafted;
        _audit.Record("Quote", 0, "Drafted", $"Quote drafted for ${fee} on opp {opportunityId}", "operator");
        await _db.SaveChangesAsync(ct);
        return quote;
    }

    public async Task SendAsync(long quoteId, CancellationToken ct)
    {
        var q = await Get(quoteId, ct);
        q.Status = QuoteStatus.Sent;
        q.SentAt = DateTimeOffset.UtcNow;
        if (q.Opportunity is { } opp) opp.Status = OpportunityStatus.QuoteSent;
        _audit.Record("Quote", q.Id, "Sent", "Quote sent", "operator");
        await _db.SaveChangesAsync(ct);
    }

    public async Task MarkPaidAsync(long quoteId, CancellationToken ct)
    {
        var q = await Get(quoteId, ct);
        q.Status = QuoteStatus.Paid;
        q.PaidAt = DateTimeOffset.UtcNow;
        if (q.Opportunity is { } opp) opp.Status = OpportunityStatus.Paid;
        _audit.Record("Quote", q.Id, "Paid", $"Payment received: ${q.Amount}", "operator");
        await _db.SaveChangesAsync(ct);
    }

    public async Task MarkOverdueAsync(long quoteId, CancellationToken ct)
    {
        var q = await Get(quoteId, ct);
        q.Status = QuoteStatus.Overdue;
        _audit.Record("Quote", q.Id, "Overdue", "Quote marked overdue", "operator");
        await _db.SaveChangesAsync(ct);
    }

    private async Task<Quote> Get(long id, CancellationToken ct) =>
        await _db.Quotes.Include(q => q.Opportunity).FirstOrDefaultAsync(q => q.Id == id, ct)
            ?? throw new InvalidOperationException("Quote not found");
}
