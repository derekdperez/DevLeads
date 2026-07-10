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

        // ---- Opportunities ----
        api.MapGet("/opportunities", async (DevLeadsDbContext db, string? status, string? priority) =>
        {
            var q = db.Opportunities.AsNoTracking().AsQueryable();
            if (Enum.TryParse<OpportunityStatus>(status, true, out var s)) q = q.Where(o => o.Status == s);
            if (Enum.TryParse<Priority>(priority, true, out var p)) q = q.Where(o => o.Priority == p);
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
                var opp = await ingest.CreateManualAsync(dto.Title, dto.Body, dto.SourceUrl ?? "", dto.Author, dto.AuthorUrl, default);
                return Results.Ok(opp);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        });

        MapStatusAction(api, "approve", OpportunityStatus.Approved);
        MapStatusAction(api, "reject", OpportunityStatus.Rejected);
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

    public record ManualLeadDto(string Title, string Body, string? SourceUrl, string? Author, string? AuthorUrl);
    public record DraftDto(string TemplateKey);
    public record QuoteDto(double? Amount, bool DueOnCompletion);
}
