using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using DevLeads.Core;
using DevLeads.Core.Entities;
using DevLeads.Infrastructure.Data;

namespace DevLeads.Infrastructure.Services;

/// <summary>
/// Client-relationship operations that involve more than one aggregate: promoting a lead
/// into a client + first engagement, and the shared "due follow-ups" queries. Plain CRUD
/// on clients/engagements/follow-ups lives in the pages, matching the rest of the app.
/// </summary>
public sealed class ClientService
{
    private readonly DevLeadsDbContext _db;
    private readonly AuditService _audit;
    private readonly ILogger<ClientService> _log;

    public ClientService(DevLeadsDbContext db, AuditService audit, ILogger<ClientService> log)
    {
        _db = db;
        _audit = audit;
        _log = log;
    }

    /// <summary>
    /// Turns a lead into a client with a prospective engagement and a default follow-up.
    /// Idempotent per lead: promoting twice returns the existing client.
    /// </summary>
    public async Task<(Client? Client, bool Created, string Message)> PromoteOpportunityAsync(long opportunityId, CancellationToken ct)
    {
        var existing = await _db.Clients.FirstOrDefaultAsync(c => c.SourceOpportunityId == opportunityId, ct);
        if (existing is not null)
            return (existing, false, $"Already a client: {existing.Name}.");

        var opp = await _db.Opportunities.AsNoTracking().FirstOrDefaultAsync(o => o.Id == opportunityId, ct);
        if (opp is null) return (null, false, "Opportunity not found.");

        var settings = await _db.OperatorSettings.AsNoTracking().FirstOrDefaultAsync(ct);
        var now = DateTimeOffset.UtcNow;
        var client = new Client
        {
            Name = string.IsNullOrWhiteSpace(opp.AuthorName) ? opp.Title : opp.AuthorName!,
            Platform = PlatformFromSource(opp),
            ProfileUrl = opp.AuthorProfileUrl ?? "",
            Status = ClientStatus.Prospect,
            SourceOpportunityId = opp.Id,
            CampaignId = opp.CampaignId,
            Notes = $"Promoted from lead: {opp.Title}\n{opp.SourceUrl}",
            CreatedAt = now, UpdatedAt = now,
            Engagements =
            {
                new Engagement
                {
                    Title = opp.Title,
                    Description = opp.Summary,
                    Status = EngagementStatus.Prospective,
                    AgreedFee = opp.FeeIsEstimate ? null : opp.EstimatedFeeMax ?? opp.EstimatedFeeMin,
                    OpportunityId = opp.Id,
                    CreatedAt = now, UpdatedAt = now
                }
            },
            FollowUps =
            {
                new FollowUp
                {
                    Note = "Check in on the conversation — keep the thread warm.",
                    DueAt = now.AddHours(settings?.FollowUpDefaultHours ?? 24),
                    CreatedAt = now
                }
            }
        };
        _db.Clients.Add(client);
        await _db.SaveChangesAsync(ct);

        _audit.Record("Opportunity", opp.Id, "PromotedToClient", $"Lead promoted to client #{client.Id} ({client.Name})", "operator");
        _audit.Record("Client", client.Id, "Created", $"Promoted from lead #{opp.Id}: {opp.Title}", "operator");
        await _db.SaveChangesAsync(ct);

        _log.LogInformation("Lead {OpportunityId} promoted to client {ClientId}", opp.Id, client.Id);
        return (client, true, $"Client created: {client.Name}.");
    }

    private static string PlatformFromSource(Opportunity opp)
    {
        var host = LeadQualityRules.HostFromUrl(opp.SourceUrl) ?? "";
        if (host.Contains("reddit")) return "reddit";
        if (host.Contains("github")) return "github";
        if (host.Contains("stackoverflow") || host.Contains("stackexchange")) return "stackoverflow";
        if (host.Contains("news.ycombinator")) return "hackernews";
        if (host.Contains("linkedin")) return "linkedin";
        if (host.Contains("upwork")) return "upwork";
        return string.IsNullOrWhiteSpace(host) ? opp.SourceKey : host;
    }
}
