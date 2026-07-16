using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using DevLeads.Core;
using DevLeads.Core.Ai;
using DevLeads.Core.Entities;
using DevLeads.Core.Skills;
using DevLeads.Core.Templates;
using DevLeads.Infrastructure.Ai;
using DevLeads.Infrastructure.Data;

namespace DevLeads.Infrastructure.Services;

/// <summary>
/// Manages the human-in-the-loop outreach queue: drafts, approvals, sends, and suppression.
/// Sending never bypasses the kill switch, suppression list, or approval requirement.
/// Response bodies are written by batched AI generation: leads are queued, then one model
/// call writes every queued reply grounded in its original post.
/// </summary>
public sealed class OutreachService
{
    private readonly DevLeadsDbContext _db;
    private readonly AuditService _audit;
    private readonly AiTextRouter _text;
    private readonly DiscoveryActivityTracker _activity;
    private readonly EmailService _email;

    public OutreachService(DevLeadsDbContext db, AuditService audit,
        AiTextRouter text, DiscoveryActivityTracker activity, EmailService email)
    {
        _db = db;
        _audit = audit;
        _text = text;
        _activity = activity;
        _email = email;
    }

    /// <summary>Placeholder body shown while an attempt waits in the generation queue.</summary>
    public const string QueuedPlaceholder = "(queued — the next AI generation run will write this reply from the original post)";

    /// <summary>
    /// Adds a lead to the AI generation queue. Idempotent: an existing queued/pending/
    /// approved attempt for the same lead is returned instead of duplicated.
    /// </summary>
    public async Task<OutreachAttempt> QueueForGenerationAsync(long opportunityId, CancellationToken ct)
    {
        var opp = await _db.Opportunities.FirstOrDefaultAsync(o => o.Id == opportunityId, ct)
                  ?? throw new InvalidOperationException("Opportunity not found");

        var existing = await _db.OutreachAttempts.FirstOrDefaultAsync(a =>
            a.OpportunityId == opportunityId &&
            (a.Status == OutreachStatus.QueuedForGeneration ||
             a.Status == OutreachStatus.PendingApproval ||
             a.Status == OutreachStatus.Approved), ct);
        if (existing is not null) return existing;

        var settings = await GetSettings(ct);
        var attempt = new OutreachAttempt
        {
            OpportunityId = opportunityId,
            Channel = OutreachChannel.ManualCopy,
            Mode = settings.DefaultOutreachMode,
            TemplateKey = "ai_batch_v1",
            Body = QueuedPlaceholder,
            Status = OutreachStatus.QueuedForGeneration,
            RequiresApproval = true,
            CreatedAt = DateTimeOffset.UtcNow
        };
        _db.OutreachAttempts.Add(attempt);
        _audit.Record("Opportunity", opportunityId, "ResponseQueued", "Lead added to the AI generation queue", "operator");
        await _db.SaveChangesAsync(ct);
        return attempt;
    }

    /// <summary>Count of attempts currently waiting in the generation queue.</summary>
    public Task<int> QueuedCountAsync(CancellationToken ct) =>
        _db.OutreachAttempts.CountAsync(a => a.Status == OutreachStatus.QueuedForGeneration, ct);

    /// <summary>
    /// Writes every queued reply in a single model call (chunked only past
    /// <see cref="GenerationChunkSize"/> items as a prompt-size guard). Each reply is
    /// grounded in the lead's original post text; generated attempts move to
    /// PendingApproval and appear in the approval queue.
    /// </summary>
    public async Task<(int Generated, string Message)> GenerateQueuedResponsesAsync(CancellationToken ct)
    {
        var queued = await _db.OutreachAttempts
            .Include(a => a.Opportunity)
            .Where(a => a.Status == OutreachStatus.QueuedForGeneration)
            .OrderBy(a => a.Id)
            .ToListAsync(ct);
        if (queued.Count == 0) return (0, "The generation queue is empty.");

        var settings = await GetSettings(ct);
        if (_text.ProviderFor(settings, AiFeature.Outreach).Equals("Heuristic", StringComparison.OrdinalIgnoreCase))
            return (0, "Response generation is set to Heuristic — choose OpenCode or Codex in Settings.");

        var skills = await _db.Skills.AsNoTracking().Where(s => s.Enabled).ToListAsync(ct);
        var operatorSkills = skills.Count == 0 ? "" : SkillMatcher.PromptSummary(skills);
        var objectives = await _db.Campaigns.AsNoTracking()
            .ToDictionaryAsync(c => c.Id, c => c.Objective, ct);

        var oppIds = queued.Select(a => a.OpportunityId).Distinct().ToList();
        var rawBodies = (await _db.RawSourceItems.AsNoTracking()
                .Where(r => r.OpportunityId != null && oppIds.Contains(r.OpportunityId.Value))
                .Select(r => new { r.OpportunityId, r.BodyText })
                .ToListAsync(ct))
            .GroupBy(r => r.OpportunityId!.Value)
            .ToDictionary(g => g.Key, g => string.Join('\n', g.Select(r => r.BodyText)));

        _activity.RunStarted("outreach_generation", $"Writing {queued.Count} queued response(s) — batched AI call");
        var generated = 0;
        var emptyBodies = 0;
        try
        {
            foreach (var chunk in queued.Chunk(GenerationChunkSize))
            {
                var items = chunk.Select((a, idx) => new OutreachGenerationItem
                {
                    Id = "r" + idx,
                    Title = a.Opportunity?.Title ?? "",
                    OriginalPost = rawBodies.GetValueOrDefault(a.OpportunityId)
                                   ?? a.Opportunity?.Summary ?? "",
                    SourceKey = a.Opportunity?.SourceKey ?? "",
                    Url = a.Opportunity?.SourceUrl ?? "",
                    AuthorName = a.Opportunity?.AuthorName,
                    CampaignObjective = a.Opportunity?.CampaignId is { } cid
                        ? objectives.GetValueOrDefault(cid, "") : ""
                }).ToList();

                var prompt = OutreachPrompts.BuildBatchResponsePrompt(items, settings, operatorSkills);
                var timeout = TimeSpan.FromSeconds(Math.Clamp(settings.AiTimeoutSeconds * 3, 180, 900));
                var (ok, text, error, model) = await _text.GenerateTextAsync(AiFeature.Outreach, prompt, settings, timeout, ct);
                if (!ok)
                {
                    _activity.RunCompleted("outreach_generation", healthy: false, "Response generation failed: " + error);
                    return (generated, "Response generation failed: " + error);
                }

                var json = OpenCodeTriageProvider.ExtractJsonObject(text);
                if (json is null)
                {
                    _activity.RunCompleted("outreach_generation", healthy: false, "Response generation returned no JSON.");
                    return (generated, "Response generation returned no parsable JSON.");
                }

                var bodies = ParseResponses(json);
                for (var i = 0; i < chunk.Length; i++)
                {
                    if (!bodies.TryGetValue("r" + i, out var body) || string.IsNullOrWhiteSpace(body))
                    {
                        emptyBodies++; // model honestly declined — leave it queued for review
                        continue;
                    }
                    var attempt = chunk[i];
                    attempt.Body = body.Trim();
                    attempt.Status = OutreachStatus.PendingApproval;
                    if (attempt.Opportunity is { } opp &&
                        opp.Status is OpportunityStatus.New or OpportunityStatus.AiTriaged
                            or OpportunityStatus.NeedsReview or OpportunityStatus.FollowUpLater)
                        opp.Status = OpportunityStatus.DraftReady;
                    generated++;
                }

                _audit.Record("OutreachAttempt", 0, "BatchGenerated",
                    $"AI ({model}) wrote {generated} of {chunk.Length} queued response(s) in one call.");
                await _db.SaveChangesAsync(ct);
            }
        }
        catch (OperationCanceledException)
        {
            _activity.RunCompleted("outreach_generation", healthy: false, "Response generation cancelled.");
            throw;
        }

        var message = $"{generated} response(s) written" +
                      (emptyBodies > 0 ? $"; {emptyBodies} left queued (model had nothing grounded to say)." : ".");
        _activity.RunCompleted("outreach_generation", healthy: true, "Outreach generation: " + message);
        return (generated, message);
    }

    /// <summary>
    /// Prompt-size guard only — the whole queue normally fits one call. 12 posts × ~1.6k
    /// chars of source text stays well inside the free models' context windows.
    /// </summary>
    public const int GenerationChunkSize = 12;

    private static Dictionary<string, string> ParseResponses(string json)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("responses", out var arr) || arr.ValueKind != JsonValueKind.Array)
                return result;
            foreach (var el in arr.EnumerateArray())
            {
                var id = el.TryGetProperty("id", out var idEl) ? idEl.GetString() ?? "" : "";
                var body = el.TryGetProperty("body", out var bodyEl) ? bodyEl.GetString() ?? "" : "";
                if (id.Length > 0) result[id] = body;
            }
        }
        catch (JsonException) { /* caller reports the empty result */ }
        return result;
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
        // A Failed attempt with an ApprovedAt stamp was already human-approved; retrying
        // the delivery does not need a second approval.
        var approved = a.Status == OutreachStatus.Approved ||
                       (a.Status == OutreachStatus.Failed && a.ApprovedAt is not null);
        if (a.RequiresApproval && !approved)
            return (false, "Outreach must be approved before sending.");

        var contact = a.Opportunity?.AuthorProfileUrl ?? a.Opportunity?.AuthorName;
        if (settings.SuppressionListEnabled && contact is not null && await IsSuppressedAsync(contact, ct))
            return (false, "Contact is on the suppression list.");

        // Real delivery when the attempt targets an email address and sending is enabled;
        // every other channel stays a manual-send record, exactly as before.
        var deliveredNote = "";
        if (a.Channel == OutreachChannel.Email && a.RecipientEmail.Length > 0 && settings.EmailSendEnabled)
        {
            var subject = string.IsNullOrWhiteSpace(a.Subject)
                ? "Following up on your post" : a.Subject!;
            var (ok, messageId, error) = await _email.SendAsync(a.RecipientEmail, subject, a.Body, ct);
            if (!ok)
            {
                a.Status = OutreachStatus.Failed;
                a.ErrorMessage = error;
                _audit.Record("OutreachAttempt", a.Id, "SendFailed", error, "operator",
                    new { a.Channel, a.RecipientEmail });
                await _db.SaveChangesAsync(ct);
                return (false, error);
            }
            a.SentMessageId = messageId;
            a.ErrorMessage = null;
            deliveredNote = $" Email delivered to {a.RecipientEmail}.";
        }

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
        return (true, $"Recorded as sent.{deliveredNote} Follow-up scheduled.");
    }

    /// <summary>
    /// Targets an attempt at a real email address so Send actually delivers it
    /// (most leads have no address — public replies stay the default).
    /// </summary>
    public async Task SetEmailRecipientAsync(long attemptId, string email, CancellationToken ct)
    {
        var a = await Get(attemptId, ct);
        a.RecipientEmail = email.Trim();
        a.Channel = a.RecipientEmail.Length > 0 ? OutreachChannel.Email : OutreachChannel.ManualCopy;
        _audit.Record("OutreachAttempt", a.Id, "RecipientSet",
            a.RecipientEmail.Length > 0 ? $"Email recipient set: {a.RecipientEmail}" : "Email recipient cleared",
            "operator");
        await _db.SaveChangesAsync(ct);
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
