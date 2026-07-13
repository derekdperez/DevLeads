using Microsoft.EntityFrameworkCore;
using DevLeads.Core;
using DevLeads.Infrastructure.Data;
using DevLeads.Infrastructure.Services;

namespace DevLeads.Web.Api;

/// <summary>Internal HTTP API used for automation and integration (the UI calls services directly).</summary>
public static class ApiEndpoints
{
    public static void MapDevLeadsApi(this WebApplication app)
    {
        // Internal automation API: JSON in/out, no browser antiforgery token required.
        var api = app.MapGroup("/api").DisableAntiforgery();

        // ---- Campaigns ----
        api.MapGet("/campaigns", async (DevLeadsDbContext db) =>
            Results.Ok(await db.Campaigns.AsNoTracking().OrderBy(c => c.Id).ToListAsync()));

        // ---- Opportunities ----
        api.MapGet("/opportunities", async (DevLeadsDbContext db, string? status, string? priority, long? campaignId) =>
        {
            var q = db.Opportunities.AsNoTracking().AsQueryable();
            if (Enum.TryParse<OpportunityStatus>(status, true, out var s)) q = q.Where(o => o.Status == s);
            if (Enum.TryParse<Priority>(priority, true, out var p)) q = q.Where(o => o.Priority == p);
            if (campaignId is { } cid) q = q.Where(o => o.CampaignId == cid);
            var list = await q.OrderByDescending(o => o.Score).Take(200).ToListAsync();
            return Results.Ok(list);
        });

        api.MapGet("/opportunities/{id:long}", async (long id, DevLeadsDbContext db) =>
        {
            var opp = await db.Opportunities.AsNoTracking()
                .Include(o => o.TriageRuns).Include(o => o.OutreachAttempts).Include(o => o.Quotes).Include(o => o.WorkSessions)
                .FirstOrDefaultAsync(o => o.Id == id);
            return opp is null ? Results.NotFound() : Results.Ok(opp);
        });

        api.MapPost("/opportunities/manual", async (ManualLeadDto dto, LeadIngestionService ingest) =>
        {
            try
            {
                var opp = await ingest.CreateManualAsync(dto.Title, dto.Body, dto.SourceUrl ?? "", dto.Author, dto.AuthorUrl, default, dto.CampaignId);
                return Results.Ok(opp);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        });

        MapStatusAction(api, "approve", OpportunityStatus.Approved);
        MapStatusAction(api, "reject", OpportunityStatus.Rejected);
        MapStatusAction(api, "archive", OpportunityStatus.Archived);
        MapStatusAction(api, "watch", OpportunityStatus.FollowUpLater);
        MapStatusAction(api, "mark-contacted", OpportunityStatus.Contacted);
        MapStatusAction(api, "mark-won", OpportunityStatus.Won);
        MapStatusAction(api, "mark-lost", OpportunityStatus.Lost);

        api.MapPost("/opportunities/{id:long}/rerun-triage", async (long id, LeadIngestionService ingest) =>
        {
            await ingest.RerunAsync(id, default);
            return Results.Ok();
        });
        api.MapPost("/opportunities/{id:long}/run-ai-triage", async (long id, LeadIngestionService ingest) =>
        {
            await ingest.RerunAsync(id, default);
            return Results.Ok();
        });
        api.MapGet("/opportunities/{id:long}/triage-runs", async (long id, DevLeadsDbContext db) =>
            Results.Ok(await db.AiTriageRuns.AsNoTracking().Where(r => r.OpportunityId == id).OrderByDescending(r => r.Id).ToListAsync()));

        // ---- Outreach ----
        api.MapPost("/opportunities/{id:long}/draft-response", async (long id, DraftDto dto, OutreachService svc) =>
            Results.Ok(await svc.GenerateDraftAsync(id, dto.TemplateKey, default)));
        api.MapPost("/outreach/{id:long}/approve", async (long id, OutreachService svc) => { await svc.ApproveAsync(id, default); return Results.Ok(); });
        api.MapPost("/outreach/{id:long}/send", async (long id, OutreachService svc) =>
        {
            var (sent, message) = await svc.SendAsync(id, default);
            return sent ? Results.Ok(new { message }) : Results.BadRequest(new { message });
        });
        api.MapPost("/outreach/{id:long}/cancel", async (long id, OutreachService svc) => { await svc.CancelAsync(id, default); return Results.Ok(); });
        api.MapPost("/opportunities/{id:long}/queue-response", async (long id, OutreachService svc) =>
            Results.Ok(await svc.QueueForGenerationAsync(id, default)));
        api.MapPost("/outreach/generate-queued", async (OutreachService svc) =>
        {
            var (generated, message) = await svc.GenerateQueuedResponsesAsync(default);
            return Results.Ok(new { generated, message });
        });

        // ---- Quotes ----
        api.MapPost("/opportunities/{id:long}/generate-quote", async (long id, QuoteDto dto, QuoteService svc) =>
            Results.Ok(await svc.GenerateAsync(id, dto.Amount, dto.DueOnCompletion, default)));
        api.MapPost("/quotes/{id:long}/send", async (long id, QuoteService svc) => { await svc.SendAsync(id, default); return Results.Ok(); });
        api.MapPost("/quotes/{id:long}/mark-paid", async (long id, QuoteService svc) => { await svc.MarkPaidAsync(id, default); return Results.Ok(); });
        api.MapPost("/quotes/{id:long}/mark-overdue", async (long id, QuoteService svc) => { await svc.MarkOverdueAsync(id, default); return Results.Ok(); });

        // ---- Sources ----
        api.MapGet("/sources", async (DevLeadsDbContext db) => Results.Ok(await db.SourceConfigs.AsNoTracking().ToListAsync()));
        api.MapPost("/sources/run-all", async (DevLeadsDbContext db, SourceRunner runner) =>
        {
            var sources = await db.SourceConfigs
                .Where(s => s.Enabled)
                .OrderBy(s => s.DisplayName)
                .ToListAsync();

            var results = new List<object>();
            var totalCreated = 0;
            foreach (var src in sources)
            {
                var created = await runner.RunAsync(src, default);
                totalCreated += created;
                results.Add(new { src.SourceKey, created, src.LastRunMessage, src.LastRunHealthy });
            }

            return Results.Ok(new { totalCreated, sourceCount = sources.Count, results });
        });
        api.MapPost("/sources/{key}/test", async (string key, SourceRunner runner) =>
            Results.Ok(await runner.CheckHealthAsync(key, default)));
        api.MapPost("/sources/{key}/run-now", async (string key, DevLeadsDbContext db, SourceRunner runner) =>
        {
            var src = await db.SourceConfigs.FirstOrDefaultAsync(s => s.SourceKey == key);
            if (src is null) return Results.NotFound();
            var created = await runner.RunAsync(src, default);
            return Results.Ok(new { created, src.LastRunMessage, src.LastRunHealthy });
        });

        // ---- Content studio ----
        api.MapPost("/content/scan", async (TrendScanService scanner) =>
            Results.Ok(new { created = await scanner.RunDueAsync(force: true, default) }));
        api.MapGet("/content/signals", async (DevLeadsDbContext db) =>
            Results.Ok(await db.TrendSignals.AsNoTracking()
                .OrderByDescending(s => s.Hotness).Take(50).ToListAsync()));
        api.MapPost("/content/topics/generate", async (ContentStudioService studio) =>
        {
            var (created, message) = await studio.GenerateTopicsAsync(5, default);
            return Results.Ok(new { created, message });
        });
        api.MapGet("/content/topics", async (DevLeadsDbContext db) =>
            Results.Ok(await db.ContentTopics.AsNoTracking().OrderByDescending(t => t.Id).Take(50).ToListAsync()));
        api.MapPost("/content/topics/{id:long}/drafts", async (long id, string? format, ContentStudioService studio) =>
        {
            if (!Enum.TryParse<ContentFormat>(format, true, out var f))
                return Results.BadRequest(new { message = "format must be one of: " + string.Join(", ", Enum.GetNames<ContentFormat>()) });
            var (draft, message) = await studio.GenerateDraftAsync(id, f, default);
            return draft is null ? Results.BadRequest(new { message }) : Results.Ok(draft);
        });
        api.MapGet("/content/drafts", async (DevLeadsDbContext db) =>
            Results.Ok(await db.ContentDrafts.AsNoTracking().OrderByDescending(d => d.Id).Take(50).ToListAsync()));

        // ---- My posts (operator's own posts on external platforms) ----
        api.MapGet("/myposts", async (DevLeadsDbContext db) =>
            Results.Ok(await db.OperatorPosts.AsNoTracking().OrderByDescending(p => p.PostedAt).ToListAsync()));
        api.MapPost("/myposts/sync-reddit", async (bool? all, OperatorPostService svc) =>
        {
            var (imported, refreshed, message) = await svc.SyncRedditAsync(jobPostsOnly: all != true, default);
            return Results.Ok(new { imported, refreshed, message });
        });
        api.MapPost("/myposts/{id:long}/summarize", async (long id, OperatorPostService svc) =>
        {
            var (ok, message) = await svc.SummarizeThreadAsync(id, default);
            return ok ? Results.Ok(new { message }) : Results.BadRequest(new { message });
        });
        api.MapPost("/myposts/draft", async (string platform, long? campaignId, string? instructions, OperatorPostService svc) =>
        {
            var (post, message) = await svc.GeneratePostAsync(platform, campaignId, instructions ?? "", default);
            return post is null ? Results.BadRequest(new { message }) : Results.Ok(post);
        });
        api.MapPost("/myposts/optimize", async (string ids, string? instructions, OperatorPostService svc) =>
        {
            var postIds = ids.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Select(s => long.TryParse(s, out var id) ? id : 0).Where(id => id > 0).ToList();
            var (created, message) = await svc.OptimizePostsAsync(postIds, instructions ?? "", default);
            return created > 0 ? Results.Ok(new { created, message }) : Results.BadRequest(new { message });
        });
        api.MapGet("/myposts/revisions", async (DevLeadsDbContext db) =>
            Results.Ok(await db.OperatorPostRevisions.AsNoTracking()
                .OrderByDescending(r => r.Id).Take(100).ToListAsync()));
        api.MapPost("/myposts/revisions/{id:long}/apply", async (long id, OperatorPostService svc) =>
        {
            var (ok, message) = await svc.ApplyRevisionAsync(id, default);
            return ok ? Results.Ok(new { message }) : Results.BadRequest(new { message });
        });
        api.MapPost("/myposts/revisions/{id:long}/dismiss", async (long id, DevLeadsDbContext db) =>
        {
            var rev = await db.OperatorPostRevisions.FirstOrDefaultAsync(r => r.Id == id);
            if (rev is null) return Results.NotFound();
            rev.Status = OperatorPostRevisionStatus.Dismissed;
            await db.SaveChangesAsync();
            return Results.Ok(rev);
        });
        api.MapGet("/myposts/messages", async (DevLeadsDbContext db) =>
            Results.Ok(await db.OperatorMessages.AsNoTracking()
                .OrderByDescending(m => m.ReceivedAt).Take(200).ToListAsync()));
        api.MapPost("/myposts/sync-inbox", async (OperatorPostService svc) =>
        {
            var (imported, message) = await svc.SyncRedditInboxAsync(default);
            return Results.Ok(new { imported, message });
        });
        api.MapPost("/myposts/messages/{id:long}/read", async (long id, OperatorPostService posts) =>
            await posts.MarkMessageReadAsync(id, default) ? Results.Ok() : Results.NotFound())
            .DisableAntiforgery();
        api.MapPost("/myposts/messages/{id:long}/status", async (
            long id, string status, DevLeadsDbContext db, OperatorPostService posts) =>
        {
            if (!Enum.TryParse<OperatorMessageStatus>(status, true, out var s))
                return Results.BadRequest(new { message = "status must be one of: " + string.Join(", ", Enum.GetNames<OperatorMessageStatus>()) });
            if (s == OperatorMessageStatus.Read)
                return await posts.MarkMessageReadAsync(id, default) ? Results.Ok() : Results.NotFound();
            var msg = await db.OperatorMessages.FirstOrDefaultAsync(m => m.Id == id);
            if (msg is null) return Results.NotFound();
            msg.Status = s;
            msg.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync();
            return Results.Ok(msg);
        }).DisableAntiforgery();

        // ---- System ----
        api.MapPost("/system/restart", async (AppRestartService restart, DevLeadsDbContext db, AuditService audit) =>
        {
            audit.Record("System", 0, "Restart", "Web server restart requested", "operator");
            await db.SaveChangesAsync();
            var error = restart.Restart();
            if (error is not null) return Results.Problem(error);
            return Results.Ok(new { message = "Restarting — rebuilding and relaunching; back in ~15–60 seconds." });
        });
    }

    private static void MapStatusAction(RouteGroupBuilder api, string action, OpportunityStatus status)
    {
        api.MapPost($"/opportunities/{{id:long}}/{action}", async (long id, DevLeadsDbContext db, AuditService audit) =>
        {
            var opp = await db.Opportunities.FirstOrDefaultAsync(o => o.Id == id);
            if (opp is null) return Results.NotFound();
            opp.Status = status;
            opp.UpdatedAt = DateTimeOffset.UtcNow;
            audit.Record("Opportunity", id, action, $"Status -> {status}", "operator");
            await db.SaveChangesAsync();
            return Results.Ok(opp);
        });
    }

    public record ManualLeadDto(string Title, string Body, string? SourceUrl, string? Author, string? AuthorUrl, long? CampaignId = null);
    public record DraftDto(string TemplateKey);
    public record QuoteDto(double? Amount, bool DueOnCompletion);
}
