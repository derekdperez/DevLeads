using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using DevLeads.Core;
using DevLeads.Core.Ai;
using DevLeads.Core.Entities;
using DevLeads.Infrastructure.Ai;
using DevLeads.Infrastructure.Data;

namespace DevLeads.Infrastructure.Services;

/// <summary>One actionable line on the Today page, with where to go to act on it.</summary>
public sealed record AgendaItem(string Icon, string Text, string Href, bool Urgent = false);

/// <summary>
/// The deterministic snapshot the Today page renders and the daily briefing reasons over:
/// what needs attention, the best open leads, live engagements, and presence health.
/// </summary>
public sealed record TodayAgenda(
    List<AgendaItem> Attention,
    List<Opportunity> TopOpportunities,
    List<(Engagement Engagement, string ClientName)> ActiveEngagements,
    List<(FollowUp FollowUp, string ClientName)> DueFollowUps,
    int LeadsLast24h,
    int UnreadMessages,
    int PendingDrafts,
    int QueuedDrafts,
    int OutstandingQuotes,
    int ActiveClients,
    int ActivePlatforms,
    int SuggestedPlatforms,
    int StaleActivePosts);

/// <summary>
/// The manager layer: assembles the zero-cost Today agenda from live data, and writes the
/// one-per-day advisor briefing (AI when available; a deterministic fallback otherwise, so
/// the morning briefing always exists).
/// </summary>
public sealed class AdvisorService
{
    /// <summary>Statuses that mean "an open lead the operator should look at".</summary>
    private static readonly OpportunityStatus[] ReviewStatuses =
    {
        OpportunityStatus.NeedsReview, OpportunityStatus.DraftReady, OpportunityStatus.Approved,
        OpportunityStatus.Responded, OpportunityStatus.Qualified
    };

    private readonly DevLeadsDbContext _db;
    private readonly AiTextRouter _text;
    private readonly AuditService _audit;
    private readonly ILogger<AdvisorService> _log;

    public AdvisorService(DevLeadsDbContext db, AiTextRouter text, AuditService audit, ILogger<AdvisorService> log)
    {
        _db = db;
        _text = text;
        _audit = audit;
        _log = log;
    }

    public async Task<TodayAgenda> BuildAgendaAsync(CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var minPostedAt = now.AddDays(-LeadQualityRules.MaxAutomatedLeadAgeDays);

        var unread = await _db.OperatorMessages.AsNoTracking()
            .Where(m => m.Status == OperatorMessageStatus.Unread)
            .OrderByDescending(m => m.ReceivedAt).ToListAsync(ct);
        var pendingDrafts = await _db.OutreachAttempts.AsNoTracking()
            .Where(a => a.Status == OutreachStatus.PendingApproval).CountAsync(ct);
        var queuedDrafts = await _db.OutreachAttempts.AsNoTracking()
            .Where(a => a.Status == OutreachStatus.QueuedForGeneration).CountAsync(ct);
        var outstandingQuotes = await _db.Quotes.AsNoTracking()
            .Where(q => q.Status == QuoteStatus.Sent || q.Status == QuoteStatus.Overdue ||
                        q.Status == QuoteStatus.PaymentPending)
            .Include(q => q.Opportunity).ToListAsync(ct);
        var dueFollowUps = (await _db.FollowUps.AsNoTracking()
                .Where(f => f.Status == FollowUpStatus.Pending && f.DueAt <= now.AddDays(1))
                .Include(f => f.Client)
                .OrderBy(f => f.DueAt).ToListAsync(ct))
            .Select(f => (f, f.Client?.Name ?? "?")).ToList();
        var activeEngagements = (await _db.Engagements.AsNoTracking()
                .Where(e => e.Status == EngagementStatus.Active || e.Status == EngagementStatus.Negotiating ||
                            e.Status == EngagementStatus.Prospective || e.Status == EngagementStatus.OnHold)
                .Include(e => e.Client)
                .OrderBy(e => e.DueAt == null).ThenBy(e => e.DueAt).ToListAsync(ct))
            .Select(e => (e, e.Client?.Name ?? "?")).ToList();
        var topOpps = (await _db.Opportunities.AsNoTracking()
                .Where(o => ReviewStatuses.Contains(o.Status) && o.PostedAt >= minPostedAt)
                .OrderByDescending(o => o.Score).Take(5).ToListAsync(ct));
        var leads24h = await _db.Opportunities.AsNoTracking()
            .Where(o => o.CreatedAt >= now.AddHours(-24)).CountAsync(ct);
        var activeClients = await _db.Clients.AsNoTracking()
            .Where(c => c.Status == ClientStatus.Active || c.Status == ClientStatus.Prospect).CountAsync(ct);
        var activePlatforms = await _db.PlatformProfiles.AsNoTracking()
            .Where(p => p.Status == PlatformPresenceStatus.Active).CountAsync(ct);
        var suggestedPlatforms = await _db.PlatformProfiles.AsNoTracking()
            .Where(p => p.Status == PlatformPresenceStatus.Suggested).CountAsync(ct);
        var staleCutoff = now.AddHours(-72);
        var stalePosts = (await _db.OperatorPosts.AsNoTracking()
                .Where(p => p.Status == OperatorPostStatus.Active).ToListAsync(ct))
            .Count(p => p.LastCheckedAt is null || p.LastCheckedAt < staleCutoff);
        var settings = await _db.OperatorSettings.AsNoTracking().FirstOrDefaultAsync(ct);
        var pendingLinkedInReplies = await _db.EngagementDrafts.AsNoTracking()
            .Where(d => d.Platform == "linkedin" && d.Status == EngagementDraftStatus.PendingReview && d.DraftText != "")
            .CountAsync(ct);
        var scheduledLinkedInPosts = await _db.OperatorPosts.AsNoTracking()
            .Where(p => p.Platform == "linkedin" && p.Status == OperatorPostStatus.Draft && p.ScheduledAt != null)
            .CountAsync(ct);

        // The "needs your attention" list, most human-urgent first: real people waiting
        // beat internal queues, and money beats both queues and new leads.
        var attention = new List<AgendaItem>();
        foreach (var m in unread.Take(5))
            attention.Add(new AgendaItem("📨", $"Unread {m.Platform} message from {m.Author}: {Truncate(m.Subject, 60)}", "/myposts", Urgent: true));
        if (pendingLinkedInReplies > 0)
            attention.Add(new AgendaItem("💬", $"{pendingLinkedInReplies} LinkedIn response draft(s) waiting for review", "/linkedin", Urgent: true));
        if (settings is not null && !string.IsNullOrWhiteSpace(settings.LinkedInClientId))
        {
            if (string.IsNullOrWhiteSpace(settings.LinkedInAccessToken) && scheduledLinkedInPosts > 0)
                attention.Add(new AgendaItem("🔗", $"Connect LinkedIn before {scheduledLinkedInPosts} scheduled post(s) become due", "/linkedin", Urgent: true));
            else if (settings.LinkedInAccessTokenExpiresAt is { } tokenExpiry && tokenExpiry <= now.AddDays(7))
            {
                var tokenText = tokenExpiry <= now ? "expired" : $"expires {tokenExpiry.LocalDateTime:MMM d}";
                attention.Add(new AgendaItem("🔑", $"LinkedIn access token {tokenText} — reconnect before scheduled publishing stops", "/linkedin", Urgent: tokenExpiry <= now));
            }
        }
        foreach (var (f, clientName) in dueFollowUps.Where(x => x.f.DueAt <= now).Take(5))
            attention.Add(new AgendaItem("⏰", $"Follow-up due — {clientName}: {Truncate(f.Note, 60)}", $"/clients/{f.ClientId}", Urgent: true));
        foreach (var q in outstandingQuotes.Where(q => q.Status == QuoteStatus.Overdue).Take(3))
            attention.Add(new AgendaItem("💸", $"Quote overdue: ${q.Amount:0} — {Truncate(q.Opportunity?.Title ?? "?", 50)}", $"/opportunities/{q.OpportunityId}", Urgent: true));
        if (pendingDrafts > 0)
            attention.Add(new AgendaItem("✉️", $"{pendingDrafts} outreach draft(s) waiting for your approval", "/drafts"));
        foreach (var (e, clientName) in activeEngagements.Where(x => x.e.DueAt is { } d && d <= now.AddDays(3)).Take(3))
            attention.Add(new AgendaItem("📦", $"Engagement due {e.DueAt!.Value.LocalDateTime:MMM d} — {clientName}: {Truncate(e.Title, 50)}", $"/clients/{e.ClientId}"));

        return new TodayAgenda(
            attention, topOpps, activeEngagements, dueFollowUps,
            leads24h, unread.Count, pendingDrafts, queuedDrafts, outstandingQuotes.Count,
            activeClients, activePlatforms, suggestedPlatforms, stalePosts);
    }

    public Task<AdvisorBriefing?> GetTodayBriefingAsync(CancellationToken ct)
    {
        var today = new DateTimeOffset(DateTimeOffset.UtcNow.UtcDateTime.Date, TimeSpan.Zero);
        return _db.AdvisorBriefings.AsNoTracking()
            .Where(b => b.ForDate == today)
            .OrderByDescending(b => b.Id)
            .FirstOrDefaultAsync(ct);
    }

    /// <summary>
    /// Writes today's briefing if it doesn't exist yet (<paramref name="force"/> rewrites
    /// it). AI when the advisor feature has a text provider; the deterministic fallback
    /// otherwise or on any AI failure — the day never ends up briefing-less.
    /// </summary>
    public async Task<(AdvisorBriefing? Briefing, string Message)> GenerateDailyBriefingAsync(
        bool force, CancellationToken ct, bool allowAi = true)
    {
        var today = new DateTimeOffset(DateTimeOffset.UtcNow.UtcDateTime.Date, TimeSpan.Zero);
        var existing = await _db.AdvisorBriefings.Where(b => b.ForDate == today)
            .OrderByDescending(b => b.Id).FirstOrDefaultAsync(ct);
        if (existing is not null && !force)
            return (existing, "Today's briefing already exists.");

        var settings = await _db.OperatorSettings.AsNoTracking().FirstOrDefaultAsync(ct) ?? new OperatorSettings();
        var agenda = await BuildAgendaAsync(ct);

        string body, provider, model, message;
        if (!allowAi ||
            _text.ProviderFor(settings, AiFeature.AdvisorBriefing).Equals("Heuristic", StringComparison.OrdinalIgnoreCase))
        {
            (body, provider, model) = (BuildFallbackBriefing(agenda), "Heuristic", "rules");
            message = allowAi
                ? "Briefing written offline (advisor feature is set to Heuristic)."
                : "Briefing written offline (AI budget exhausted).";
        }
        else
        {
            var prompt = AdvisorPrompts.BuildDailyBriefingPrompt(settings, BuildAgendaContext(agenda));
            var timeout = TimeSpan.FromSeconds(Math.Clamp(settings.AiTimeoutSeconds * 2, 120, 600));
            var (ok, text, error, usedModel) = await _text.GenerateTextAsync(AiFeature.AdvisorBriefing, prompt, settings, timeout, ct);
            if (ok && !string.IsNullOrWhiteSpace(text))
            {
                (body, provider, model) = (text.Trim(), settings.AiFor(AiFeature.AdvisorBriefing).Provider, usedModel);
                message = "Today's briefing is ready.";
            }
            else
            {
                _log.LogWarning("Advisor briefing AI call failed, using fallback: {Error}", error);
                (body, provider, model) = (BuildFallbackBriefing(agenda), "Heuristic", "rules");
                message = "AI was unavailable — wrote the offline briefing instead. " + error;
            }
        }

        var now = DateTimeOffset.UtcNow;
        if (existing is not null)
        {
            existing.BodyMarkdown = body;
            existing.Provider = provider;
            existing.Model = model;
            existing.CreatedAt = now;
        }
        else
        {
            existing = new AdvisorBriefing
            {
                ForDate = today, BodyMarkdown = body,
                Provider = provider, Model = model, CreatedAt = now
            };
            _db.AdvisorBriefings.Add(existing);
        }
        await _db.SaveChangesAsync(ct);
        _audit.Record("AdvisorBriefing", existing.Id, "BriefingWritten", $"Daily briefing via {provider}/{model}.", "system");
        await _db.SaveChangesAsync(ct);
        return (existing, message);
    }

    /// <summary>Compact factual snapshot the AI is allowed to reason over.</summary>
    private static string BuildAgendaContext(TodayAgenda a)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Counts: {a.LeadsLast24h} new leads in 24h; {a.UnreadMessages} unread messages; {a.PendingDrafts} drafts awaiting approval; {a.QueuedDrafts} replies queued for generation; {a.OutstandingQuotes} quotes outstanding; {a.ActiveClients} active/prospect clients; {a.ActivePlatforms} active platforms, {a.SuggestedPlatforms} suggested and unreviewed; {a.StaleActivePosts} live posts unchecked for 72h+.");
        if (a.Attention.Count > 0)
        {
            sb.AppendLine("Needs attention now:");
            foreach (var item in a.Attention.Take(10)) sb.AppendLine($"- {item.Text}");
        }
        if (a.TopOpportunities.Count > 0)
        {
            sb.AppendLine("Best open leads (score 0-100):");
            foreach (var o in a.TopOpportunities)
                sb.AppendLine($"- [{o.Score:0}] {Truncate(o.Title, 80)} ({o.Status}; {o.PaymentIntent} pay intent{(o.FeeIsEstimate ? "" : $"; stated ${o.EstimatedFeeMax ?? o.EstimatedFeeMin:0}")})");
        }
        if (a.ActiveEngagements.Count > 0)
        {
            sb.AppendLine("Client engagements in flight:");
            foreach (var (e, name) in a.ActiveEngagements.Take(6))
                sb.AppendLine($"- {name}: {Truncate(e.Title, 60)} ({e.Status}{(e.DueAt is { } d ? $", due {d.LocalDateTime:MMM d}" : "")}{(string.IsNullOrWhiteSpace(e.NextDeliverable) ? "" : $"; next: {Truncate(e.NextDeliverable, 50)}")})");
        }
        if (a.DueFollowUps.Count > 0)
        {
            sb.AppendLine("Follow-ups due:");
            foreach (var (f, name) in a.DueFollowUps.Take(6))
                sb.AppendLine($"- {name}: {Truncate(f.Note, 60)} (due {f.DueAt.LocalDateTime:MMM d})");
        }
        return sb.ToString();
    }

    /// <summary>The no-AI briefing: the same priorities the prompt encodes, applied by rule.</summary>
    private static string BuildFallbackBriefing(TodayAgenda a)
    {
        var actions = new List<string>();
        if (a.UnreadMessages > 0)
            actions.Add($"**Answer your {a.UnreadMessages} unread message(s)** — a person who already reached out beats any new lead.");
        var overdue = a.Attention.Where(x => x.Urgent && x.Icon is "⏰" or "💸").ToList();
        foreach (var item in overdue.Take(2))
            actions.Add($"**{item.Text}**");
        if (a.PendingDrafts > 0)
            actions.Add($"**Approve or edit the {a.PendingDrafts} waiting outreach draft(s)** — drafted replies decay fast on live threads.");
        if (a.TopOpportunities.Count > 0)
        {
            var top = a.TopOpportunities[0];
            actions.Add($"**Review the top lead ({top.Score:0}): {Truncate(top.Title, 70)}**");
        }
        if (actions.Count < 3 && a.SuggestedPlatforms > 0)
            actions.Add($"**Review {a.SuggestedPlatforms} suggested platform(s)** on My posts — pick one and post there this week.");
        if (actions.Count < 3 && a.StaleActivePosts > 0)
            actions.Add($"**Refresh {a.StaleActivePosts} stale live post(s)** — check replies and update view counts.");

        var sb = new StringBuilder();
        sb.AppendLine("## Top 3 today");
        foreach (var action in actions.Take(3)) sb.AppendLine($"1. {action}");
        if (actions.Count == 0) sb.AppendLine("1. Quiet board — run discovery, then spend the time on presence: post or engage on one platform.");
        sb.AppendLine();
        sb.AppendLine("## Pipeline read");
        sb.AppendLine($"{a.LeadsLast24h} new lead(s) in the last 24h, {a.PendingDrafts} draft(s) awaiting approval, {a.QueuedDrafts} queued for generation, {a.OutstandingQuotes} quote(s) outstanding, {a.ActiveClients} active client relationship(s).");
        sb.AppendLine();
        sb.AppendLine("## Presence");
        sb.AppendLine($"{a.ActivePlatforms} platform(s) active, {a.SuggestedPlatforms} suggestion(s) unreviewed, {a.StaleActivePosts} live post(s) not checked in 72h.");
        sb.AppendLine();
        sb.AppendLine("_Written offline (no AI call). Use “Regenerate with AI” for the full briefing._");
        return sb.ToString();
    }

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max] + "…";
}
