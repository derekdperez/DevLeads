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

        // ---- Clients & engagements ----
        api.MapGet("/clients", async (DevLeadsDbContext db) =>
            Results.Ok(await db.Clients.AsNoTracking()
                .Include(c => c.Engagements).Include(c => c.FollowUps)
                .OrderByDescending(c => c.UpdatedAt).Take(200).ToListAsync()));
        api.MapGet("/clients/{id:long}", async (long id, DevLeadsDbContext db) =>
        {
            var client = await db.Clients.AsNoTracking()
                .Include(c => c.Engagements).Include(c => c.Interactions).Include(c => c.FollowUps)
                .FirstOrDefaultAsync(c => c.Id == id);
            return client is null ? Results.NotFound() : Results.Ok(client);
        });
        api.MapPost("/opportunities/{id:long}/promote-to-client", async (long id, ClientService svc) =>
        {
            var (client, created, message) = await svc.PromoteOpportunityAsync(id, default);
            return client is null ? Results.BadRequest(new { message }) : Results.Ok(new { client.Id, created, message });
        });

        // ---- Advisor (Today) ----
        api.MapGet("/advisor/briefing", async (AdvisorService advisor) =>
        {
            var briefing = await advisor.GetTodayBriefingAsync(default);
            return briefing is null ? Results.NotFound(new { message = "No briefing for today yet." }) : Results.Ok(briefing);
        });
        api.MapPost("/advisor/briefing/generate", async (bool? force, AdvisorService advisor) =>
        {
            var (briefing, message) = await advisor.GenerateDailyBriefingAsync(force == true, default);
            return briefing is null ? Results.BadRequest(new { message }) : Results.Ok(new { briefing, message });
        });

        // ---- Platform presence ----
        api.MapGet("/platforms", async (DevLeadsDbContext db, string? status) =>
        {
            var q = db.PlatformProfiles.AsNoTracking().AsQueryable();
            if (Enum.TryParse<PlatformPresenceStatus>(status, true, out var s)) q = q.Where(p => p.Status == s);
            return Results.Ok(await q.OrderBy(p => p.Name).ToListAsync());
        });
        api.MapPost("/platforms/discover", async (PlatformPresenceService svc) =>
        {
            var (created, message) = await svc.DiscoverPlatformsAsync(default);
            return Results.Ok(new { created, message });
        });
        // Signup packs: pass ids for specific platforms; omit ids to sweep every
        // suggested/planned platform without a pack. Batched ~5 platforms per AI call.
        api.MapPost("/platforms/signup-packs/generate", async (string? ids, long? campaignId, string? instructions, PlatformPresenceService svc) =>
        {
            var profileIds = ids?.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Select(s => long.TryParse(s, out var id) ? id : 0).Where(id => id > 0).ToList();
            var (generated, calls, message) = await svc.GenerateSignupPacksAsync(
                profileIds is { Count: > 0 } ? profileIds : null, campaignId, instructions ?? "", default);
            return generated > 0 ? Results.Ok(new { generated, calls, message }) : Results.BadRequest(new { message });
        });
        api.MapPost("/platforms/{id:long}/status", async (long id, string status, DevLeadsDbContext db, PlatformPresenceService svc) =>
        {
            if (!Enum.TryParse<PlatformPresenceStatus>(status, true, out var s))
                return Results.BadRequest(new { message = "status must be one of: " + string.Join(", ", Enum.GetNames<PlatformPresenceStatus>()) });
            // Activation goes through the service so the pack's first post becomes a tracked draft.
            if (s == PlatformPresenceStatus.Active)
            {
                var (ok, message) = await svc.ActivateAsync(id, default);
                return ok ? Results.Ok(new { message }) : Results.NotFound(new { message });
            }
            var row = await db.PlatformProfiles.FirstOrDefaultAsync(p => p.Id == id);
            if (row is null) return Results.NotFound();
            row.Status = s;
            row.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync();
            return Results.Ok(row);
        });

        // ---- Documents (resume today; future kinds slot in beside it) ----
        api.MapGet("/documents/{kind}", async (string kind, DevLeadsDbContext db) =>
        {
            var doc = await db.OperatorDocuments.AsNoTracking().FirstOrDefaultAsync(d => d.Kind == kind);
            return doc is null
                ? Results.NotFound(new { message = $"No {kind} uploaded — add one on the Skill profile page." })
                : Results.File(doc.Data, doc.ContentType, doc.FileName);
        });
        api.MapPost("/documents/{kind}", async (string kind, IFormFile file, DevLeadsDbContext db) =>
        {
            if (file.Length is 0 or > 15 * 1024 * 1024)
                return Results.BadRequest(new { message = "File must be between 1 byte and 15 MB." });
            var doc = await db.OperatorDocuments.FirstOrDefaultAsync(d => d.Kind == kind);
            if (doc is null) { doc = new Core.Entities.OperatorDocument { Kind = kind }; db.OperatorDocuments.Add(doc); }
            using var buffer = new MemoryStream();
            await file.CopyToAsync(buffer);
            doc.FileName = file.FileName;
            doc.ContentType = string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType;
            doc.SizeBytes = file.Length;
            doc.Data = buffer.ToArray();
            doc.UploadedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync();
            return Results.Ok(new { doc.Kind, doc.FileName, doc.SizeBytes, doc.UploadedAt });
        }).DisableAntiforgery();
        api.MapDelete("/documents/{kind}", async (string kind, DevLeadsDbContext db) =>
        {
            var doc = await db.OperatorDocuments.FirstOrDefaultAsync(d => d.Kind == kind);
            if (doc is null) return Results.NotFound();
            db.OperatorDocuments.Remove(doc);
            await db.SaveChangesAsync();
            return Results.Ok();
        });

        // ---- LinkedIn profile, publishing, and reviewed engagement ----
        api.MapGet("/linkedin/status", async (LinkedInService linkedIn) =>
            Results.Ok(await linkedIn.GetConnectionStatusAsync(default)));
        api.MapGet("/linkedin/authorize", async (HttpRequest request, LinkedInService linkedIn) =>
        {
            var callback = $"{request.Scheme}://{request.Host}{request.PathBase}/api/linkedin/callback";
            var (url, message) = await linkedIn.CreateAuthorizationUrlAsync(callback, default);
            // Connect is a plain link, so surface config errors as a page flash, not raw JSON.
            return url is null
                ? Results.Redirect("/linkedin?oauthError=" + Uri.EscapeDataString(message))
                : Results.Redirect(url);
        });
        api.MapGet("/linkedin/callback", async (
            HttpRequest request, string? code, string? state, string? error,
            string? error_description, LinkedInService linkedIn) =>
        {
            if (!string.IsNullOrWhiteSpace(error))
                return Results.Redirect("/linkedin?oauthError=" + Uri.EscapeDataString(error_description ?? error));
            var callback = $"{request.Scheme}://{request.Host}{request.PathBase}/api/linkedin/callback";
            var (ok, message) = await linkedIn.CompleteOAuthAsync(code ?? "", state ?? "", callback, default);
            var key = ok ? "oauth" : "oauthError";
            return Results.Redirect($"/linkedin?{key}={Uri.EscapeDataString(message)}");
        });
        api.MapPost("/linkedin/disconnect", async (LinkedInService linkedIn) =>
        {
            await linkedIn.DisconnectAsync(default);
            return Results.Ok(new { message = "LinkedIn disconnected." });
        });
        api.MapPost("/linkedin/publish/{id:long}", async (long id, LinkedInService linkedIn) =>
        {
            var (ok, message) = await linkedIn.PublishPostAsync(id, default);
            return ok ? Results.Ok(new { message }) : Results.BadRequest(new { message });
        });
        api.MapPost("/linkedin/publish-due", async (LinkedInService linkedIn) =>
        {
            var (published, failed, message) = await linkedIn.PublishDueAsync(default);
            return Results.Ok(new { published, failed, message });
        });
        api.MapPost("/linkedin/engagement/sync", async (LinkedInService linkedIn) =>
        {
            var (imported, checkedPosts, message) = await linkedIn.SyncEngagementAsync(default);
            return Results.Ok(new { imported, checkedPosts, message });
        });
        api.MapPost("/linkedin/engagement/generate", async (string? instructions, LinkedInService linkedIn) =>
        {
            var (generated, message) = await linkedIn.GenerateEngagementBatchAsync(instructions ?? "", default);
            return generated > 0 ? Results.Ok(new { generated, message }) : Results.BadRequest(new { message });
        });
        api.MapPost("/linkedin/engagement/{id:long}/publish", async (long id, LinkedInService linkedIn) =>
        {
            var (ok, message) = await linkedIn.PublishEngagementAsync(id, default);
            return ok ? Results.Ok(new { message }) : Results.BadRequest(new { message });
        });
        api.MapGet("/linkedin/actions", async (DevLeadsDbContext db) =>
        {
            var settings = await db.OperatorSettings.AsNoTracking().FirstOrDefaultAsync();
            var actions = await db.LinkedInActions.AsNoTracking()
                .OrderBy(a => a.Status == Core.LinkedInActionStatus.Pending ? 0
                    : a.Status == Core.LinkedInActionStatus.Done ? 1 : 2)
                .ThenBy(a => a.SortOrder).ToListAsync();
            return Results.Ok(new
            {
                review = settings?.LinkedInProfileReview ?? "",
                reviewAt = settings?.LinkedInProfileReviewAt,
                actions
            });
        });
        api.MapPost("/linkedin/actions/generate", async (string? instructions, LinkedInService linkedIn) =>
        {
            var (created, message) = await linkedIn.GenerateActionPlanAsync(instructions ?? "", default);
            return created > 0 ? Results.Ok(new { created, message }) : Results.BadRequest(new { message });
        });
        MapLinkedInActionStatus(api, "done", Core.LinkedInActionStatus.Done);
        MapLinkedInActionStatus(api, "dismiss", Core.LinkedInActionStatus.Dismissed);
        MapLinkedInActionStatus(api, "reopen", Core.LinkedInActionStatus.Pending);

        // ---- Discord (bot posting, channel monitoring, and reviewed replies) ----
        api.MapGet("/discord/status", async (DiscordService discord) =>
            Results.Ok(await discord.GetStatusAsync(default)));
        api.MapGet("/discord/channels", async (DevLeadsDbContext db) =>
            Results.Ok(await db.DiscordChannels.AsNoTracking()
                .OrderBy(c => c.GuildName).ThenBy(c => c.ChannelName).ToListAsync()));
        api.MapPost("/discord/channels/sync", async (DiscordService discord) =>
        {
            var (guilds, channels, message) = await discord.SyncChannelsAsync(default);
            return Results.Ok(new { guilds, channels, message });
        });
        api.MapPost("/discord/channels/{id:long}/monitor", async (long id, bool enabled, DevLeadsDbContext db) =>
        {
            var row = await db.DiscordChannels.FirstOrDefaultAsync(c => c.Id == id);
            if (row is null) return Results.NotFound();
            row.MonitorEnabled = enabled;
            row.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync();
            return Results.Ok(row);
        });
        api.MapPost("/discord/publish/{id:long}", async (long id, DiscordService discord) =>
        {
            var (ok, message) = await discord.PublishPostAsync(id, default);
            return ok ? Results.Ok(new { message }) : Results.BadRequest(new { message });
        });
        api.MapPost("/discord/publish-due", async (DiscordService discord) =>
        {
            var (published, failed, message) = await discord.PublishDueAsync(default);
            return Results.Ok(new { published, failed, message });
        });
        api.MapPost("/discord/track", async (string link, DiscordService discord) =>
        {
            var (ok, message) = await discord.TrackMessageAsync(link, default);
            return ok ? Results.Ok(new { message }) : Results.BadRequest(new { message });
        });
        api.MapPost("/discord/engagement/sync", async (DiscordService discord) =>
        {
            var (imported, checkedChannels, message) = await discord.SyncEngagementAsync(default);
            return Results.Ok(new { imported, checkedChannels, message });
        });
        api.MapPost("/discord/engagement/generate", async (string? instructions, DiscordService discord) =>
        {
            var (generated, message) = await discord.GenerateEngagementBatchAsync(instructions ?? "", default);
            return generated > 0 ? Results.Ok(new { generated, message }) : Results.BadRequest(new { message });
        });
        api.MapPost("/discord/engagement/{id:long}/publish", async (long id, DiscordService discord) =>
        {
            var (ok, message) = await discord.PublishEngagementAsync(id, default);
            return ok ? Results.Ok(new { message }) : Results.BadRequest(new { message });
        });

        // ---- Site rescue (broken business web assets → repair leads) ----
        api.MapGet("/webscan/probes", async (DevLeadsDbContext db) =>
            Results.Ok(await db.WebScanProbes.AsNoTracking().OrderByDescending(p => p.UpdatedAt).ToListAsync()));
        api.MapGet("/webscan/findings", async (DevLeadsDbContext db, string? status) =>
        {
            var q = db.WebAssetFindings.AsNoTracking().AsQueryable();
            if (Enum.TryParse<WebAssetStatus>(status, true, out var s)) q = q.Where(f => f.Status == s);
            var list = await q.OrderByDescending(f => f.FirstSeenAt).Take(300).ToListAsync();
            return Results.Ok(list);
        });
        api.MapPost("/webscan/scan", async (WebScanRunDto dto, WebRescueService rescue) =>
        {
            var (checkedCount, found, message) = await rescue.ScanAsync(
                dto.ProbeId, dto.Targets ?? "", dto.UseDiscovery, default);
            return Results.Ok(new { checkedCount, found, message });
        });
        api.MapPost("/webscan/generate", async (string? instructions, WebRescueService rescue) =>
        {
            var (generated, message) = await rescue.GenerateOutreachBatchAsync(instructions ?? "", default);
            return generated > 0 ? Results.Ok(new { generated, message }) : Results.BadRequest(new { message });
        });
        api.MapPost("/webscan/findings/{id:long}/recheck", async (long id, WebRescueService rescue) =>
        {
            var (ok, message) = await rescue.RecheckAsync(id, default);
            return ok ? Results.Ok(new { message }) : Results.BadRequest(new { message });
        });
        api.MapPost("/webscan/findings/{id:long}/refresh-contact", async (long id, WebRescueService rescue) =>
        {
            var (ok, message) = await rescue.RefreshContactAsync(id, default);
            return ok ? Results.Ok(new { message }) : Results.BadRequest(new { message });
        });
        api.MapPost("/webscan/findings/{id:long}/status/{status}", async (long id, string status, DevLeadsDbContext db, AuditService audit) =>
        {
            if (!Enum.TryParse<WebAssetStatus>(status, true, out var target)) return Results.BadRequest(new { message = "Unknown status." });
            var row = await db.WebAssetFindings.FirstOrDefaultAsync(f => f.Id == id);
            if (row is null) return Results.NotFound();
            row.Status = target;
            row.UpdatedAt = DateTimeOffset.UtcNow;
            audit.Record("WebAssetFinding", id, "Status", $"Status -> {target}", "operator");
            await db.SaveChangesAsync();
            return Results.Ok(row);
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

    private static void MapLinkedInActionStatus(RouteGroupBuilder api, string action, Core.LinkedInActionStatus status)
    {
        api.MapPost($"/linkedin/actions/{{id:long}}/{action}", async (long id, DevLeadsDbContext db) =>
        {
            var row = await db.LinkedInActions.FirstOrDefaultAsync(a => a.Id == id);
            if (row is null) return Results.NotFound();
            row.Status = status;
            row.CompletedAt = status == Core.LinkedInActionStatus.Done ? DateTimeOffset.UtcNow : null;
            row.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync();
            return Results.Ok(row);
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
    public record WebScanRunDto(long ProbeId, string? Targets, bool UseDiscovery);
}
