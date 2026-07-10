using Microsoft.EntityFrameworkCore;
using DevLeads.Core;
using DevLeads.Core.Entities;
using DevLeads.Core.Templates;
using DevLeads.Infrastructure.Data;

namespace DevLeads.Infrastructure.Services;

/// <summary>
/// Manages the human-in-the-loop outreach queue: drafts, approvals, sends, and suppression.
/// Sending never bypasses the kill switch, suppression list, or approval requirement.
/// </summary>
public sealed class OutreachService
{
    private readonly DevLeadsDbContext _db;
    private readonly AuditService _audit;

    public OutreachService(DevLeadsDbContext db, AuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    public async Task<OutreachAttempt> GenerateDraftAsync(long opportunityId, string templateKey, CancellationToken ct)
    {
        var opp = await _db.Opportunities.FirstOrDefaultAsync(o => o.Id == opportunityId, ct)
                  ?? throw new InvalidOperationException("Opportunity not found");
        var settings = await GetSettings(ct);
        var template = ResponseTemplates.Get(templateKey);
        var body = template.Body
            .Replace("[specific issue]", opp.Title)
            .Replace("[likely cause/category]", string.IsNullOrWhiteSpace(opp.ProblemType) ? "a production issue" : opp.ProblemType.ToLowerInvariant())
            .Replace("[specific diagnostic step]", string.IsNullOrWhiteSpace(opp.SuggestedFirstStep) ? "the most likely failure point" : opp.SuggestedFirstStep);

        var attempt = new OutreachAttempt
        {
            OpportunityId = opportunityId,
            Channel = template.Channel == "PublicReply" ? OutreachChannel.PublicReply
                    : template.Channel == "Email" ? OutreachChannel.GmailDraft : OutreachChannel.ManualCopy,
            Mode = settings.DefaultOutreachMode,
            TemplateKey = templateKey,
            Subject = template.Channel == "Email" ? "Emergency software fix — " + opp.Title : null,
            Body = body,
            Status = OutreachStatus.PendingApproval,
            RequiresApproval = true,
            CreatedAt = DateTimeOffset.UtcNow
        };
        _db.OutreachAttempts.Add(attempt);
        if (opp.Status is OpportunityStatus.New or OpportunityStatus.AiTriaged or OpportunityStatus.NeedsReview)
            opp.Status = OpportunityStatus.DraftReady;
        _audit.Record("OutreachAttempt", 0, "DraftGenerated", $"Draft via {templateKey} for opp {opportunityId}", "operator");
        await _db.SaveChangesAsync(ct);
        return attempt;
    }

    public async Task ApproveAsync(long attemptId, CancellationToken ct)
    {
        var a = await Get(attemptId, ct);
        a.Status = OutreachStatus.Approved;
        a.ApprovedAt = DateTimeOffset.UtcNow;
        if (a.Opportunity is { } opp && opp.Status == OpportunityStatus.DraftReady)
            opp.Status = OpportunityStatus.Approved;
        _audit.Record("OutreachAttempt", a.Id, "Approved", "Operator approved outreach", "operator");
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// "Sends" the outreach. In this MVP this records a Gmail draft / manual send and marks the
    /// opportunity contacted; it never transmits automatically without passing all safety gates.
    /// </summary>
    public async Task<(bool Sent, string Message)> SendAsync(long attemptId, CancellationToken ct)
    {
        var a = await Get(attemptId, ct);
        var settings = await GetSettings(ct);

        if (settings.GlobalKillSwitch)
            return (false, "Global kill switch is ON — no outbound messages.");
        if (a.RequiresApproval && a.Status != OutreachStatus.Approved)
            return (false, "Outreach must be approved before sending.");

        var contact = a.Opportunity?.AuthorProfileUrl ?? a.Opportunity?.AuthorName;
        if (settings.SuppressionListEnabled && contact is not null && await IsSuppressedAsync(contact, ct))
            return (false, "Contact is on the suppression list.");

        a.Status = OutreachStatus.Sent;
        a.SentAt = DateTimeOffset.UtcNow;
        if (a.Opportunity is { } opp)
        {
            opp.Status = OpportunityStatus.Contacted;
            opp.NextFollowUpAt = DateTimeOffset.UtcNow.AddHours(settings.FollowUpDefaultHours);
        }
        _audit.Record("OutreachAttempt", a.Id, "Sent", $"Outreach recorded as sent ({a.Channel})", "operator",
            new { a.Channel, a.Mode });
        await _db.SaveChangesAsync(ct);
        return (true, "Recorded as sent. Follow-up scheduled.");
    }

    public async Task CancelAsync(long attemptId, CancellationToken ct)
    {
        var a = await Get(attemptId, ct);
        a.Status = OutreachStatus.Cancelled;
        _audit.Record("OutreachAttempt", a.Id, "Cancelled", "Operator cancelled outreach", "operator");
        await _db.SaveChangesAsync(ct);
    }

    public async Task MarkRespondedAsync(long opportunityId, CancellationToken ct)
    {
        var opp = await _db.Opportunities.Include(o => o.OutreachAttempts)
            .FirstOrDefaultAsync(o => o.Id == opportunityId, ct) ?? throw new InvalidOperationException("Not found");
        opp.Status = OpportunityStatus.Responded;
        var latest = opp.OutreachAttempts.OrderByDescending(x => x.CreatedAt).FirstOrDefault();
        if (latest is not null) { latest.Status = OutreachStatus.Responded; latest.ResponseReceivedAt = DateTimeOffset.UtcNow; }
        _audit.Record("Opportunity", opportunityId, "Responded", "Lead responded", "operator");
        await _db.SaveChangesAsync(ct);
    }

    public Task<bool> IsSuppressedAsync(string contact, CancellationToken ct) =>
        _db.SuppressionEntries.AnyAsync(s => s.ContactValue == contact, ct);

    public async Task AddSuppressionAsync(string contact, SuppressionContactType type, string reason, CancellationToken ct)
    {
        if (!await _db.SuppressionEntries.AnyAsync(s => s.ContactValue == contact, ct))
        {
            _db.SuppressionEntries.Add(new SuppressionEntry
            {
                ContactValue = contact, ContactType = type, Reason = reason, Source = "operator",
                CreatedAt = DateTimeOffset.UtcNow
            });
            _audit.Record("Suppression", 0, "Added", $"Suppressed {contact}: {reason}", "operator");
            await _db.SaveChangesAsync(ct);
        }
    }

    private async Task<OperatorSettings> GetSettings(CancellationToken ct) =>
        await _db.OperatorSettings.FirstOrDefaultAsync(ct) ?? new OperatorSettings();

    private async Task<OutreachAttempt> Get(long id, CancellationToken ct) =>
        await _db.OutreachAttempts.Include(a => a.Opportunity).FirstOrDefaultAsync(a => a.Id == id, ct)
            ?? throw new InvalidOperationException("Outreach attempt not found");
}
