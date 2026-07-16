using System.Text;
using System.Text.RegularExpressions;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Net.Smtp;
using MailKit.Search;
using MailKit.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MimeKit;
using MimeKit.Utils;
using DevLeads.Core;
using DevLeads.Core.Entities;
using DevLeads.Infrastructure.Data;

namespace DevLeads.Infrastructure.Services;

/// <summary>
/// Real email delivery and reply ingestion over the operator's Gmail account (app
/// password; SMTP send, IMAP read). Every send re-checks the global kill switch, the
/// suppression list, and the hourly/daily send caps — callers provide approval, this
/// service provides the safety gates. Inbox sync imports ONLY messages that correlate
/// to something the app sent (reply headers or a contacted address), never the
/// operator's whole personal inbox.
/// </summary>
public sealed class EmailService
{
    private const string SmtpHost = "smtp.gmail.com";
    private const int SmtpPort = 465;
    private const string ImapHost = "imap.gmail.com";
    private const int ImapPort = 993;
    private const int MaxImportedBodyChars = 4000;

    private static readonly Regex OptOutRegex = new(
        @"\b(unsubscribe|remove me|stop (email|emailing|contacting)|opt.?out|do not (email|contact))\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly DevLeadsDbContext _db;
    private readonly AuditService _audit;
    private readonly ILogger<EmailService> _log;

    public EmailService(DevLeadsDbContext db, AuditService audit, ILogger<EmailService> log)
    {
        _db = db;
        _audit = audit;
        _log = log;
    }

    /// <summary>Sending address after the ContactEmail fallback.</summary>
    public static string ResolveAddress(OperatorSettings s) =>
        string.IsNullOrWhiteSpace(s.GmailAddress) ? s.ContactEmail.Trim() : s.GmailAddress.Trim();

    // Google renders app passwords with spaces ("abcd efgh …"); strip them on use.
    private static string ResolvePassword(OperatorSettings s) => s.GmailAppPassword.Replace(" ", "").Trim();

    private static string ResolveSenderName(OperatorSettings s) =>
        string.IsNullOrWhiteSpace(s.EmailSenderName) ? s.OperatorName : s.EmailSenderName.Trim();

    public static bool IsConfigured(OperatorSettings s) =>
        ResolveAddress(s).Length > 0 && ResolvePassword(s).Length > 0;

    /// <summary>
    /// Sends one plain-text email with the operator signature and opt-out line appended.
    /// Returns the RFC Message-Id on success so the caller can correlate future replies.
    /// </summary>
    public async Task<(bool Ok, string MessageId, string Error)> SendAsync(
        string to, string subject, string body, CancellationToken ct)
    {
        var settings = await GetSettings(ct);

        if (settings.GlobalKillSwitch)
            return (false, "", "Global kill switch is ON — no outbound messages.");
        if (!settings.EmailSendEnabled)
            return (false, "", "Email sending is disabled in Settings.");
        if (!IsConfigured(settings))
            return (false, "", "Gmail address / app password not configured in Settings.");
        to = to.Trim();
        if (to.Length == 0 || !MailboxAddress.TryParse(to, out var toAddress))
            return (false, "", $"'{to}' is not a valid email address.");

        if (settings.SuppressionListEnabled && await IsSuppressedAsync(to, ct))
            return (false, "", "Recipient is on the suppression list.");

        var (hourCount, dayCount) = await RecentSendCountsAsync(ct);
        if (hourCount >= settings.MaxSendsPerHour)
            return (false, "", $"Hourly send cap reached ({settings.MaxSendsPerHour}/h). Try again later.");
        if (dayCount >= settings.MaxSendsPerDay)
            return (false, "", $"Daily send cap reached ({settings.MaxSendsPerDay}/day). Try again tomorrow.");

        var fromAddress = ResolveAddress(settings);
        var message = new MimeMessage { MessageId = MimeUtils.GenerateMessageId() };
        message.From.Add(new MailboxAddress(ResolveSenderName(settings), fromAddress));
        message.To.Add(toAddress);
        message.Subject = subject.Trim();
        message.Body = new TextPart("plain") { Text = ComposeBody(settings, body) };

        try
        {
            using var client = new SmtpClient();
            await client.ConnectAsync(SmtpHost, SmtpPort, SecureSocketOptions.SslOnConnect, ct);
            await client.AuthenticateAsync(fromAddress, ResolvePassword(settings), ct);
            await client.SendAsync(message, ct);
            await client.DisconnectAsync(true, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _log.LogWarning(ex, "Email send to {To} failed", to);
            return (false, "", $"Send failed: {ex.Message}");
        }

        _audit.Record("Email", 0, "EmailSent", $"→ {to}: {message.Subject}", "operator",
            new { message.MessageId });
        await _db.SaveChangesAsync(ct);
        return (true, message.MessageId, "");
    }

    /// <summary>SMTP + IMAP credential check; nothing is sent.</summary>
    public async Task<(bool SmtpOk, bool ImapOk, string Message)> TestConnectionAsync(CancellationToken ct)
    {
        var settings = await GetSettings(ct);
        if (!IsConfigured(settings))
            return (false, false, "Gmail address / app password not configured.");

        var address = ResolveAddress(settings);
        var password = ResolvePassword(settings);
        bool smtpOk = false, imapOk = false;
        var notes = new List<string>();

        try
        {
            using var smtp = new SmtpClient();
            await smtp.ConnectAsync(SmtpHost, SmtpPort, SecureSocketOptions.SslOnConnect, ct);
            await smtp.AuthenticateAsync(address, password, ct);
            await smtp.DisconnectAsync(true, ct);
            smtpOk = true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            notes.Add($"SMTP: {ex.Message}");
        }

        try
        {
            using var imap = new ImapClient();
            await imap.ConnectAsync(ImapHost, ImapPort, SecureSocketOptions.SslOnConnect, ct);
            await imap.AuthenticateAsync(address, password, ct);
            await imap.DisconnectAsync(true, ct);
            imapOk = true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            notes.Add($"IMAP: {ex.Message}");
        }

        var message = smtpOk && imapOk
            ? $"Connected as {address}: SMTP ok, IMAP ok."
            : string.Join(" ", notes);
        return (smtpOk, imapOk, message);
    }

    /// <summary>
    /// Imports replies to app-sent email from the Gmail inbox. Idempotent by RFC
    /// Message-Id (existing rows are never touched, so local read state survives
    /// re-syncs). Correlated opportunities flip to Responded; correlated Site-rescue
    /// findings flip to Responded; opt-out replies add a suppression entry.
    /// </summary>
    public async Task<(int Imported, string Message)> SyncInboxAsync(CancellationToken ct)
    {
        var settings = await GetSettings(ct);
        if (!settings.EmailInboxPollEnabled)
            return (0, "Email inbox polling is disabled.");
        if (!IsConfigured(settings))
            return (0, "Gmail address / app password not configured.");

        var ownAddress = ResolveAddress(settings);
        var now = DateTimeOffset.UtcNow;

        // Correlation targets: everything the app has delivered, plus contacted addresses.
        var attempts = await _db.OutreachAttempts.AsNoTracking()
            .Where(a => a.SentMessageId != "")
            .Select(a => new { a.Id, a.OpportunityId, a.SentMessageId })
            .ToListAsync(ct);
        var attemptByMessageId = attempts.ToDictionary(a => a.SentMessageId, StringComparer.OrdinalIgnoreCase);
        var findings = await _db.WebAssetFindings.AsNoTracking()
            .Where(f => f.OutreachMessageId != "" || (f.Status == WebAssetStatus.Contacted && f.ContactEmail != ""))
            .Select(f => new { f.Id, f.OutreachMessageId, f.ContactEmail, f.Status })
            .ToListAsync(ct);
        var findingByMessageId = findings.Where(f => f.OutreachMessageId.Length > 0)
            .ToDictionary(f => f.OutreachMessageId, StringComparer.OrdinalIgnoreCase);
        var recipientEmails = await _db.OutreachAttempts.AsNoTracking()
            .Where(a => a.RecipientEmail != "" && a.SentAt != null)
            .Select(a => new { a.OpportunityId, a.RecipientEmail })
            .ToListAsync(ct);

        var known = (await _db.OperatorMessages.AsNoTracking()
                .Where(m => m.Platform == "email").Select(m => m.ExternalId).ToListAsync(ct))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var imported = 0;
        try
        {
            using var imap = new ImapClient();
            await imap.ConnectAsync(ImapHost, ImapPort, SecureSocketOptions.SslOnConnect, ct);
            await imap.AuthenticateAsync(ownAddress, ResolvePassword(settings), ct);
            var inbox = imap.Inbox;
            await inbox.OpenAsync(FolderAccess.ReadOnly, ct);

            var since = now.AddDays(-7).UtcDateTime;
            var uids = await inbox.SearchAsync(SearchQuery.DeliveredAfter(since), ct);
            if (uids.Count > 0)
            {
                var summaries = await inbox.FetchAsync(uids,
                    MessageSummaryItems.UniqueId | MessageSummaryItems.Envelope | MessageSummaryItems.References, ct);

                foreach (var summary in summaries)
                {
                    ct.ThrowIfCancellationRequested();
                    var envelope = summary.Envelope;
                    if (envelope is null) continue;

                    var externalId = !string.IsNullOrWhiteSpace(envelope.MessageId)
                        ? envelope.MessageId!
                        : $"imap:{inbox.UidValidity}:{summary.UniqueId}";
                    if (known.Contains(externalId)) continue;

                    var from = envelope.From.Mailboxes.FirstOrDefault();
                    var fromAddress = from?.Address?.Trim() ?? "";
                    if (fromAddress.Length == 0 ||
                        fromAddress.Equals(ownAddress, StringComparison.OrdinalIgnoreCase))
                        continue;

                    // Which app-sent email is this a reply to?
                    var refs = new List<string>();
                    if (summary.References is not null) refs.AddRange(summary.References);
                    if (!string.IsNullOrWhiteSpace(envelope.InReplyTo)) refs.Add(envelope.InReplyTo!);

                    long? opportunityId = null;
                    long? findingId = null;
                    foreach (var r in refs)
                    {
                        if (opportunityId is null && attemptByMessageId.TryGetValue(r, out var a))
                            opportunityId = a.OpportunityId;
                        if (findingId is null && findingByMessageId.TryGetValue(r, out var f))
                            findingId = f.Id;
                    }
                    // Header match failed (some clients drop References): fall back to the sender
                    // being an address the app has contacted.
                    findingId ??= findings.FirstOrDefault(f =>
                        f.Status == WebAssetStatus.Contacted &&
                        f.ContactEmail.Equals(fromAddress, StringComparison.OrdinalIgnoreCase))?.Id;
                    opportunityId ??= recipientEmails.FirstOrDefault(a =>
                        a.RecipientEmail.Equals(fromAddress, StringComparison.OrdinalIgnoreCase))?.OpportunityId;

                    // Not a reply to anything we sent → personal mail; stays out of the app.
                    if (opportunityId is null && findingId is null) continue;

                    var message = await inbox.GetMessageAsync(summary.UniqueId, ct);
                    var body = (message.TextBody ?? message.HtmlBody ?? "").Trim();
                    if (body.Length > MaxImportedBodyChars) body = body[..MaxImportedBodyChars];
                    var subject = envelope.Subject ?? "";

                    _db.OperatorMessages.Add(new OperatorMessage
                    {
                        Platform = "email",
                        ExternalId = externalId,
                        Kind = OperatorMessageKind.Email,
                        Author = from?.Name is { Length: > 0 } name ? $"{name} <{fromAddress}>" : fromAddress,
                        Subject = subject,
                        Body = body,
                        Status = OperatorMessageStatus.Unread,
                        OpportunityId = opportunityId,
                        WebAssetFindingId = findingId,
                        ReceivedAt = envelope.Date ?? now,
                        CreatedAt = now,
                        UpdatedAt = now
                    });
                    known.Add(externalId);
                    imported++;

                    await ApplyReplyEffectsAsync(opportunityId, findingId, fromAddress,
                        subject + "\n" + body, settings, ct);
                }
            }
            await imap.DisconnectAsync(true, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _log.LogWarning(ex, "Email inbox sync failed");
            return (imported, $"Inbox sync failed: {ex.Message}");
        }

        if (imported > 0)
        {
            _audit.Record("OperatorMessage", 0, "InboxSynced", $"Imported {imported} email repl(ies).");
            await _db.SaveChangesAsync(ct);
        }
        return (imported, imported > 0 ? $"Imported {imported} email repl(ies)." : "No new replies.");
    }

    /// <summary>Status flips + opt-out enforcement for one correlated inbound reply.</summary>
    private async Task ApplyReplyEffectsAsync(long? opportunityId, long? findingId,
        string fromAddress, string text, OperatorSettings settings, CancellationToken ct)
    {
        if (opportunityId is { } oppId)
        {
            // Same flip as OutreachService.MarkRespondedAsync, inlined to keep this
            // service free of an OutreachService dependency (which injects EmailService).
            var opp = await _db.Opportunities.Include(o => o.OutreachAttempts)
                .FirstOrDefaultAsync(o => o.Id == oppId, ct);
            if (opp is not null && opp.Status != OpportunityStatus.Responded)
            {
                opp.Status = OpportunityStatus.Responded;
                var latest = opp.OutreachAttempts.OrderByDescending(x => x.CreatedAt).FirstOrDefault();
                if (latest is not null)
                {
                    latest.Status = OutreachStatus.Responded;
                    latest.ResponseReceivedAt = DateTimeOffset.UtcNow;
                }
                _audit.Record("Opportunity", oppId, "Responded", $"Email reply received from {fromAddress}");
            }
        }

        if (findingId is { } fId)
        {
            var finding = await _db.WebAssetFindings.FirstOrDefaultAsync(f => f.Id == fId, ct);
            if (finding is not null && finding.Status is WebAssetStatus.Contacted or WebAssetStatus.Reviewing)
            {
                finding.Status = WebAssetStatus.Responded;
                finding.UpdatedAt = DateTimeOffset.UtcNow;
                _audit.Record("WebAssetFinding", fId, "Responded", $"Email reply received from {fromAddress}");
            }
        }

        if (OptOutRegex.IsMatch(text) &&
            !await _db.SuppressionEntries.AnyAsync(s => s.ContactValue == fromAddress, ct))
        {
            _db.SuppressionEntries.Add(new SuppressionEntry
            {
                ContactValue = fromAddress,
                ContactType = SuppressionContactType.Email,
                Reason = "Email opt-out reply",
                Source = "email inbox sync",
                CreatedAt = DateTimeOffset.UtcNow
            });
            _audit.Record("SuppressionEntry", 0, "OptOut", $"{fromAddress} opted out by email reply");
        }
    }

    private string ComposeBody(OperatorSettings settings, string body)
    {
        var sb = new StringBuilder(body.TrimEnd());
        sb.Append("\n\n--\n").Append(ResolveSenderName(settings));
        if (!string.IsNullOrWhiteSpace(settings.EmailSignature))
            sb.Append('\n').Append(settings.EmailSignature.Trim());
        if (!string.IsNullOrWhiteSpace(settings.BookingLink) && !body.Contains(settings.BookingLink.Trim()))
            sb.Append("\nBook a call: ").Append(settings.BookingLink.Trim());
        sb.Append("\nIf you'd rather not hear from me again, just reply \"unsubscribe\".");
        return sb.ToString();
    }

    private async Task<(int Hour, int Day)> RecentSendCountsAsync(CancellationToken ct)
    {
        // Count from the delivery timestamps, not audit rows — robust to audit logging
        // being disabled.
        var hourCutoff = DateTimeOffset.UtcNow.AddHours(-1);
        var dayCutoff = DateTimeOffset.UtcNow.AddDays(-1);
        var attemptsHour = await _db.OutreachAttempts
            .CountAsync(a => a.SentMessageId != "" && a.SentAt > hourCutoff, ct);
        var attemptsDay = await _db.OutreachAttempts
            .CountAsync(a => a.SentMessageId != "" && a.SentAt > dayCutoff, ct);
        var findingsHour = await _db.WebAssetFindings.CountAsync(f => f.OutreachSentAt > hourCutoff, ct);
        var findingsDay = await _db.WebAssetFindings.CountAsync(f => f.OutreachSentAt > dayCutoff, ct);
        return (attemptsHour + findingsHour, attemptsDay + findingsDay);
    }

    private async Task<bool> IsSuppressedAsync(string email, CancellationToken ct)
    {
        var domain = email.Contains('@') ? email[(email.IndexOf('@') + 1)..] : "";
        return await _db.SuppressionEntries.AnyAsync(
            s => s.ContactValue == email || (domain != "" && s.ContactValue == domain), ct);
    }

    private async Task<OperatorSettings> GetSettings(CancellationToken ct) =>
        await _db.OperatorSettings.FirstOrDefaultAsync(ct) ?? new OperatorSettings();
}
