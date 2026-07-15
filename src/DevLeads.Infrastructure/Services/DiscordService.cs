using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using DevLeads.Core;
using DevLeads.Core.Ai;
using DevLeads.Core.Entities;
using DevLeads.Core.Skills;
using DevLeads.Infrastructure.Ai;
using DevLeads.Infrastructure.Connectors;
using DevLeads.Infrastructure.Data;

namespace DevLeads.Infrastructure.Services;

/// <summary>
/// Discord bot integration: posting the operator's ads/offers to channels the bot was
/// invited to, monitoring those channels for replies and mentions, tracking engagement
/// over time (snapshots feed the My-posts charts), and human-reviewed reply drafts.
/// Everything uses the official Bot API — the operator's personal account is never
/// automated (self-bots violate Discord ToS), so posting into servers that won't admit
/// the bot stays manual (draft here, paste there, track the link).
/// </summary>
public sealed class DiscordService
{
    private const string ApiRoot = "https://discord.com/api/v10";

    /// <summary>View Channels + Send Messages + Read Message History.</summary>
    private const long InvitePermissions = 1024 + 2048 + 65536;

    /// <summary>Discord rejects message content over 2000 characters.</summary>
    public const int MaxMessageLength = 2000;

    // Bot REST limits are generous, but monitored channels are polled in a loop —
    // spacing the calls keeps a many-channel sync comfortably under the bucket.
    private static readonly TimeSpan RequestPacing = TimeSpan.FromMilliseconds(1200);

    private readonly DevLeadsDbContext _db;
    private readonly IHttpClientFactory _httpFactory;
    private readonly AiTextRouter _text;
    private readonly AuditService _audit;
    private readonly ILogger<DiscordService> _log;

    public DiscordService(DevLeadsDbContext db, IHttpClientFactory httpFactory,
        AiTextRouter text, AuditService audit, ILogger<DiscordService> log)
    {
        _db = db;
        _httpFactory = httpFactory;
        _text = text;
        _audit = audit;
        _log = log;
    }

    public sealed record BotStatus(
        bool Configured, bool Connected, string BotName, string BotUserId,
        string InviteUrl, int ChannelCount, int MonitoredCount, string Error);

    /// <summary>Verifies the bot token against /users/@me and caches the bot identity.</summary>
    public async Task<BotStatus> GetStatusAsync(CancellationToken ct)
    {
        var s = await GetSettingsAsync(tracking: true, ct);
        var channels = await _db.DiscordChannels.AsNoTracking().CountAsync(c => !c.Stale, ct);
        var monitored = await _db.DiscordChannels.AsNoTracking().CountAsync(c => c.MonitorEnabled && !c.Stale, ct);
        var invite = string.IsNullOrWhiteSpace(s.DiscordApplicationId) ? "" :
            $"https://discord.com/oauth2/authorize?client_id={s.DiscordApplicationId.Trim()}&scope=bot&permissions={InvitePermissions}";
        if (string.IsNullOrWhiteSpace(s.DiscordBotToken))
            return new BotStatus(false, false, "", "", invite, channels, monitored, "");

        var http = _httpFactory.CreateClient(ConnectorSupport.HttpClientName);
        using var request = BotRequest(HttpMethod.Get, "/users/@me", s);
        using var response = await http.SendAsync(request, ct);
        var json = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
            return new BotStatus(true, false, s.DiscordBotName, s.DiscordBotUserId, invite, channels, monitored,
                $"Discord rejected the bot token (HTTP {(int)response.StatusCode}): {ApiError(json)}");
        if (!TryJson(json, out var root))
            return new BotStatus(true, false, "", "", invite, channels, monitored, "Discord returned invalid JSON.");

        var id = GetString(root, "id");
        var name = GetString(root, "username");
        if (s.DiscordBotUserId != id || s.DiscordBotName != name)
        {
            s.DiscordBotUserId = id;
            s.DiscordBotName = name;
            await _db.SaveChangesAsync(ct);
        }
        return new BotStatus(true, true, name, id, invite, channels, monitored, "");
    }

    /// <summary>Refreshes the channel catalog from every server the bot is a member of.</summary>
    public async Task<(int Guilds, int Channels, string Message)> SyncChannelsAsync(CancellationToken ct)
    {
        var s = await GetSettingsAsync(tracking: false, ct);
        if (string.IsNullOrWhiteSpace(s.DiscordBotToken))
            return (0, 0, "Save the Discord bot token first.");

        var http = _httpFactory.CreateClient(ConnectorSupport.HttpClientName);
        using var guildsRequest = BotRequest(HttpMethod.Get, "/users/@me/guilds", s);
        using var guildsResponse = await http.SendAsync(guildsRequest, ct);
        var guildsJson = await guildsResponse.Content.ReadAsStringAsync(ct);
        if (!guildsResponse.IsSuccessStatusCode)
            return (0, 0, $"Could not list the bot's servers (HTTP {(int)guildsResponse.StatusCode}): {ApiError(guildsJson)}");
        if (!TryJson(guildsJson, out var guilds) || guilds.ValueKind != JsonValueKind.Array)
            return (0, 0, "Discord returned an unexpected server list.");

        var known = await _db.DiscordChannels.ToListAsync(ct);
        var seen = new HashSet<string>();
        var now = DateTimeOffset.UtcNow;
        var guildCount = 0;
        foreach (var guild in guilds.EnumerateArray())
        {
            var guildId = GetString(guild, "id");
            var guildName = GetString(guild, "name");
            if (guildId.Length == 0) continue;
            guildCount++;
            await Task.Delay(RequestPacing, ct);
            using var channelsRequest = BotRequest(HttpMethod.Get, $"/guilds/{guildId}/channels", s);
            using var channelsResponse = await http.SendAsync(channelsRequest, ct);
            var channelsJson = await channelsResponse.Content.ReadAsStringAsync(ct);
            if (!channelsResponse.IsSuccessStatusCode)
            {
                _log.LogWarning("Discord channel list failed for guild {Guild}: HTTP {Status} {Error}",
                    guildName, (int)channelsResponse.StatusCode, ApiError(channelsJson));
                continue;
            }
            if (!TryJson(channelsJson, out var channelList) || channelList.ValueKind != JsonValueKind.Array)
                continue;
            foreach (var channel in channelList.EnumerateArray())
            {
                // Type 0 = text, 5 = announcement; everything else can't hold the operator's posts.
                var type = GetInt64(channel, "type", -1);
                if (type is not (0 or 5)) continue;
                var channelId = GetString(channel, "id");
                if (channelId.Length == 0) continue;
                seen.Add(channelId);
                var row = known.FirstOrDefault(c => c.ChannelId == channelId);
                if (row is null)
                {
                    row = new DiscordChannel { ChannelId = channelId, CreatedAt = now };
                    _db.DiscordChannels.Add(row);
                    known.Add(row);
                }
                row.GuildId = guildId;
                row.GuildName = guildName;
                row.ChannelName = GetString(channel, "name");
                row.Stale = false;
                row.UpdatedAt = now;
            }
        }
        // The bot can no longer see these; keep the rows (history/notes) but flag them.
        foreach (var row in known.Where(c => !seen.Contains(c.ChannelId)))
        {
            row.Stale = true;
            row.UpdatedAt = now;
        }
        await _db.SaveChangesAsync(ct);
        return (guildCount, seen.Count, $"Found {seen.Count} text channel(s) across {guildCount} server(s).");
    }

    /// <summary>Publishes one Draft operator post to its selected Discord channel.</summary>
    public async Task<(bool Succeeded, string Message)> PublishPostAsync(long postId, CancellationToken ct)
    {
        var post = await _db.OperatorPosts.FirstOrDefaultAsync(p => p.Id == postId, ct);
        if (post is null) return (false, "Discord draft not found.");
        if (!post.Platform.Equals("discord", StringComparison.OrdinalIgnoreCase))
            return (false, "Only Discord drafts can be published here.");
        if (post.Status != OperatorPostStatus.Draft)
            return (false, $"Post is {post.Status}, not Draft.");
        var content = post.Body.Trim();
        if (content.Length == 0) return (false, "Post body is empty.");
        if (content.Length > MaxMessageLength)
            return (false, $"Discord messages max out at {MaxMessageLength} characters; this draft is {content.Length}.");

        var channel = await _db.DiscordChannels.AsNoTracking()
            .FirstOrDefaultAsync(c => c.ChannelId == post.DiscordChannelId, ct);
        if (channel is null) return (false, "Pick a target channel for this draft first.");

        var s = await GetSettingsAsync(tracking: false, ct);
        if (string.IsNullOrWhiteSpace(s.DiscordBotToken)) return (false, "Save the Discord bot token first.");
        if (s.GlobalKillSwitch) return (false, "Global kill switch is on; outbound publishing is blocked.");

        var (ok, messageId, error) = await SendMessageAsync(s, channel.ChannelId, content, replyToMessageId: null, ct);
        if (!ok) return (false, "Discord publish failed: " + error);

        var now = DateTimeOffset.UtcNow;
        post.ExternalId = messageId;
        post.Url = $"https://discord.com/channels/{channel.GuildId}/{channel.ChannelId}/{messageId}";
        post.Community = $"{channel.GuildName} · #{channel.ChannelName}";
        post.Status = OperatorPostStatus.Active;
        post.PostedAt = now;
        post.ScheduledAt = null;
        post.LastCheckedAt = now;
        post.UpdatedAt = now;
        await _db.SaveChangesAsync(ct);
        _audit.Record("OperatorPost", post.Id, "DiscordPublished",
            $"Published Discord post {messageId} in {post.Community}.", "operator");
        await _db.SaveChangesAsync(ct);
        return (true, $"Posted in {post.Community}.");
    }

    /// <summary>Publishes every due Discord draft; one failure does not block later rows.</summary>
    public async Task<(int Published, int Failed, string Message)> PublishDueAsync(CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var dueIds = await _db.OperatorPosts.AsNoTracking()
            .Where(p => p.Platform == "discord" && p.Status == OperatorPostStatus.Draft &&
                        p.ScheduledAt != null && p.ScheduledAt <= now)
            .OrderBy(p => p.ScheduledAt).Select(p => p.Id).Take(10).ToListAsync(ct);
        var published = 0;
        var failed = 0;
        foreach (var id in dueIds)
        {
            var (ok, message) = await PublishPostAsync(id, ct);
            if (ok) published++;
            else
            {
                failed++;
                _log.LogWarning("Scheduled Discord post {PostId} was not published: {Message}", id, message);
            }
        }
        return (published, failed, $"{published} scheduled Discord post(s) published; {failed} failed.");
    }

    /// <summary>
    /// Starts tracking a message the operator posted with their own account (in a server
    /// the bot can read) from its pasted message link.
    /// </summary>
    public async Task<(bool Succeeded, string Message)> TrackMessageAsync(string messageLink, CancellationToken ct)
    {
        var parts = (messageLink ?? "").Trim().TrimEnd('/').Split('/');
        // https://discord.com/channels/{guildId}/{channelId}/{messageId}
        var anchor = Array.IndexOf(parts, "channels");
        if (anchor < 0 || parts.Length < anchor + 4)
            return (false, "Paste a full Discord message link (right-click the message → Copy Message Link).");
        var guildId = parts[anchor + 1];
        var channelId = parts[anchor + 2];
        var messageId = parts[anchor + 3];

        if (await _db.OperatorPosts.AnyAsync(p => p.Platform == "discord" && p.ExternalId == messageId, ct))
            return (false, "That message is already tracked.");

        var s = await GetSettingsAsync(tracking: false, ct);
        if (string.IsNullOrWhiteSpace(s.DiscordBotToken)) return (false, "Save the Discord bot token first.");

        var http = _httpFactory.CreateClient(ConnectorSupport.HttpClientName);
        using var request = BotRequest(HttpMethod.Get, $"/channels/{channelId}/messages/{messageId}", s);
        using var response = await http.SendAsync(request, ct);
        var json = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
            return (false, $"The bot could not read that message (HTTP {(int)response.StatusCode}): {ApiError(json)}. It must be in a server and channel the bot can see.");
        if (!TryJson(json, out var message2)) return (false, "Discord returned invalid JSON.");

        var channel = await _db.DiscordChannels.AsNoTracking().FirstOrDefaultAsync(c => c.ChannelId == channelId, ct);
        var body = GetString(message2, "content");
        var now = DateTimeOffset.UtcNow;
        var postedAt = DateTimeOffset.TryParse(GetString(message2, "timestamp"), out var ts) ? ts : now;
        _db.OperatorPosts.Add(new OperatorPost
        {
            Platform = "discord",
            ExternalId = messageId,
            DiscordChannelId = channelId,
            Url = $"https://discord.com/channels/{guildId}/{channelId}/{messageId}",
            Title = body.Length > 60 ? body[..60] + "…" : (body.Length > 0 ? body : "Tracked Discord post"),
            Body = body,
            Community = channel is null ? "" : $"{channel.GuildName} · #{channel.ChannelName}",
            Status = OperatorPostStatus.Active,
            PostedAt = postedAt, LastCheckedAt = now, CreatedAt = now, UpdatedAt = now
        });
        await _db.SaveChangesAsync(ct);
        return (true, "Tracking the Discord post. Enable monitoring on its channel to collect replies.");
    }

    /// <summary>
    /// Polls monitored channels for new messages: replies to tracked posts and mentions of
    /// the bot/operator become PendingReview engagement drafts, tracked posts' reply and
    /// reaction counts are refreshed, and changed counts are snapshotted for the charts.
    /// Bot DMs are not polled (REST bots can't enumerate DM channels); DMs received on the
    /// operator's personal account are pasted manually, as everywhere else in the app.
    /// </summary>
    public async Task<(int Imported, int CheckedChannels, string Message)> SyncEngagementAsync(CancellationToken ct)
    {
        var s = await GetSettingsAsync(tracking: false, ct);
        if (string.IsNullOrWhiteSpace(s.DiscordBotToken))
            return (0, 0, "Save the Discord bot token first.");

        var channels = await _db.DiscordChannels
            .Where(c => c.MonitorEnabled && !c.Stale).OrderBy(c => c.LastSyncedAt).Take(15).ToListAsync(ct);
        if (channels.Count == 0)
            return (0, 0, "No monitored channels. Sync servers and enable monitoring on the channels you post in.");

        var tracked = await _db.OperatorPosts
            .Where(p => p.Platform == "discord" && p.Status == OperatorPostStatus.Active && p.ExternalId != "")
            .ToListAsync(ct);
        var trackedByMessageId = tracked
            .Where(p => ulong.TryParse(p.ExternalId, out _))
            .ToDictionary(p => p.ExternalId);

        var http = _httpFactory.CreateClient(ConnectorSupport.HttpClientName);
        var now = DateTimeOffset.UtcNow;
        var imported = 0;
        var checkedChannels = 0;
        foreach (var channel in channels)
        {
            ct.ThrowIfCancellationRequested();
            var query = channel.LastSyncedMessageId.Length > 0
                ? $"?after={channel.LastSyncedMessageId}&limit=100" : "?limit=100";
            using var request = BotRequest(HttpMethod.Get, $"/channels/{channel.ChannelId}/messages{query}", s);
            using var response = await http.SendAsync(request, ct);
            var json = await response.Content.ReadAsStringAsync(ct);
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                _log.LogWarning("Discord rate-limited the engagement sync; stopping this run.");
                break;
            }
            if (!response.IsSuccessStatusCode)
            {
                _log.LogWarning("Discord messages failed for #{Channel}: HTTP {Status} {Error}",
                    channel.ChannelName, (int)response.StatusCode, ApiError(json));
                await Task.Delay(RequestPacing, ct);
                continue;
            }
            if (!TryJson(json, out var messages) || messages.ValueKind != JsonValueKind.Array)
                continue;

            var firstSync = channel.LastSyncedMessageId.Length == 0;
            ulong maxId = 0;
            foreach (var message in messages.EnumerateArray())
            {
                var messageId = GetString(message, "id");
                if (ulong.TryParse(messageId, out var numeric) && numeric > maxId) maxId = numeric;

                var author = message.TryGetProperty("author", out var authorEl) ? authorEl : default;
                var authorId = GetString(author, "id");
                if (authorId.Length == 0 || authorId == s.DiscordBotUserId || authorId == s.DiscordUserId)
                    continue;
                if (author.ValueKind == JsonValueKind.Object &&
                    author.TryGetProperty("bot", out var botFlag) && botFlag.ValueKind == JsonValueKind.True)
                    continue;
                var receivedAt = DateTimeOffset.TryParse(GetString(message, "timestamp"), out var ts) ? ts : now;
                // A first sync has no cursor — seed it without dredging old history into the inbox.
                if (firstSync && now - receivedAt > TimeSpan.FromDays(7)) continue;

                OperatorPost? repliedPost = null;
                if (message.TryGetProperty("message_reference", out var reference) &&
                    reference.ValueKind == JsonValueKind.Object)
                    trackedByMessageId.TryGetValue(GetString(reference, "message_id"), out repliedPost);

                var mentioned = false;
                if (repliedPost is null && message.TryGetProperty("mentions", out var mentions) &&
                    mentions.ValueKind == JsonValueKind.Array)
                    mentioned = mentions.EnumerateArray().Any(m =>
                        GetString(m, "id") is { Length: > 0 } mid &&
                        (mid == s.DiscordBotUserId || (s.DiscordUserId.Length > 0 && mid == s.DiscordUserId)));
                if (repliedPost is null && !mentioned) continue;

                var text = GetString(message, "content");
                if (text.Length == 0) continue;
                if (await _db.EngagementDrafts.AnyAsync(d => d.Platform == "discord" && d.ExternalId == messageId, ct))
                    continue;

                var authorName = GetString(author, "global_name");
                if (authorName.Length == 0) authorName = GetString(author, "username");
                _db.EngagementDrafts.Add(new EngagementDraft
                {
                    Platform = "discord",
                    Kind = repliedPost is not null ? EngagementDraftKind.CommentReply : EngagementDraftKind.MentionReply,
                    ExternalId = messageId,
                    OperatorPostId = repliedPost?.Id,
                    // Discord identity: the channel is the thread, the message is the reply target.
                    ThreadUrn = channel.ChannelId,
                    ParentCommentUrn = messageId,
                    AuthorUrn = authorId,
                    AuthorName = authorName,
                    SourceText = text,
                    SourceUrl = $"https://discord.com/channels/{channel.GuildId}/{channel.ChannelId}/{messageId}",
                    ReceivedAt = receivedAt,
                    CreatedAt = now,
                    UpdatedAt = now
                });
                if (repliedPost is not null)
                {
                    repliedPost.ReplyCount++;
                    repliedPost.UpdatedAt = now;
                    _db.OperatorPostSnapshots.Add(new OperatorPostSnapshot
                    {
                        OperatorPostId = repliedPost.Id, At = now,
                        ReplyCount = repliedPost.ReplyCount,
                        UpvoteCount = repliedPost.UpvoteCount,
                        ViewCount = repliedPost.ViewCount
                    });
                }
                imported++;
            }
            if (maxId > 0) channel.LastSyncedMessageId = maxId.ToString();
            channel.LastSyncedAt = now;
            channel.UpdatedAt = now;
            checkedChannels++;
            await _db.SaveChangesAsync(ct);
            await Task.Delay(RequestPacing, ct);
        }

        var refreshed = await RefreshReactionsAsync(s, http, tracked, ct);
        return (imported, checkedChannels,
            $"Checked {checkedChannels} channel(s); imported {imported} new message(s); refreshed reactions on {refreshed} post(s).");
    }

    /// <summary>Refreshes reaction counts (tracked as upvotes) on the stalest active posts.</summary>
    private async Task<int> RefreshReactionsAsync(
        OperatorSettings s, HttpClient http, List<OperatorPost> tracked, CancellationToken ct)
    {
        var candidates = tracked
            .Where(p => p.DiscordChannelId.Length > 0 && ulong.TryParse(p.ExternalId, out _))
            .OrderBy(p => p.LastCheckedAt ?? DateTimeOffset.MinValue).Take(10).ToList();
        var refreshed = 0;
        foreach (var post in candidates)
        {
            ct.ThrowIfCancellationRequested();
            using var request = BotRequest(HttpMethod.Get,
                $"/channels/{post.DiscordChannelId}/messages/{post.ExternalId}", s);
            using var response = await http.SendAsync(request, ct);
            var json = await response.Content.ReadAsStringAsync(ct);
            var now = DateTimeOffset.UtcNow;
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                // The message is gone (deleted by mods or the operator).
                post.Status = OperatorPostStatus.Removed;
                post.LastCheckedAt = now;
                post.UpdatedAt = now;
                refreshed++;
                await Task.Delay(RequestPacing, ct);
                continue;
            }
            if (response.StatusCode == HttpStatusCode.TooManyRequests) break;
            if (!response.IsSuccessStatusCode || !TryJson(json, out var message)) continue;

            var reactions = 0;
            if (message.TryGetProperty("reactions", out var reactionList) &&
                reactionList.ValueKind == JsonValueKind.Array)
                reactions = reactionList.EnumerateArray().Sum(r => (int)GetInt64(r, "count", 0));
            if (reactions != post.UpvoteCount)
            {
                post.UpvoteCount = reactions;
                _db.OperatorPostSnapshots.Add(new OperatorPostSnapshot
                {
                    OperatorPostId = post.Id, At = now,
                    ReplyCount = post.ReplyCount, UpvoteCount = reactions, ViewCount = post.ViewCount
                });
            }
            post.LastCheckedAt = now;
            post.UpdatedAt = now;
            refreshed++;
            await Task.Delay(RequestPacing, ct);
        }
        await _db.SaveChangesAsync(ct);
        return refreshed;
    }

    /// <summary>Syncs what is available, then drafts all undrafted pending responses in one AI call.</summary>
    public async Task<(int Generated, string Message)> GenerateEngagementBatchAsync(
        string extraInstructions, CancellationToken ct)
    {
        var (_, _, syncMessage) = await SyncEngagementAsync(ct);
        var candidates = await _db.EngagementDrafts
            .Include(d => d.Post)
            .Where(d => d.Platform == "discord" && d.Status == EngagementDraftStatus.PendingReview && d.DraftText == "")
            .OrderBy(d => d.ReceivedAt).Take(20).ToListAsync(ct);
        if (candidates.Count == 0)
            return (0, "No undrafted Discord engagements. " + syncMessage);

        var settings = await GetSettingsAsync(tracking: false, ct);
        var skills = await _db.Skills.AsNoTracking().Where(s => s.Enabled).ToListAsync(ct);
        var items = candidates.Select(d => new DiscordPrompts.EngagementItem(
            d.Id, d.Kind, d.AuthorName, d.SourceText,
            d.Post?.Title ?? "", d.Post?.Body ?? "", d.Post?.Community ?? "")).ToList();
        var prompt = DiscordPrompts.BuildEngagementBatchPrompt(
            settings, SkillMatcher.PromptSummary(skills), items, extraInstructions);
        var timeout = TimeSpan.FromSeconds(Math.Clamp(settings.AiTimeoutSeconds * 2, 120, 600));
        var (ok, output, error, model) = await _text.GenerateTextAsync(
            AiFeature.DiscordEngagement, prompt, settings, timeout, ct);
        if (!ok) return (0, "Discord reply drafting failed: " + error);

        Dictionary<long, string> replies;
        try { replies = ParseReplies(output); }
        catch (Exception ex) { return (0, "Discord reply drafting returned invalid JSON: " + ex.Message); }

        var provider = settings.AiFor(AiFeature.DiscordEngagement).Provider;
        var generated = 0;
        var now = DateTimeOffset.UtcNow;
        foreach (var draft in candidates)
        {
            if (!replies.TryGetValue(draft.Id, out var reply) || string.IsNullOrWhiteSpace(reply)) continue;
            draft.DraftText = reply.Trim();
            draft.Provider = provider;
            draft.Model = model;
            draft.LastError = "";
            draft.UpdatedAt = now;
            generated++;
        }
        await _db.SaveChangesAsync(ct);
        _audit.Record("EngagementDraft", 0, "DiscordBatchDrafted",
            $"Generated {generated} Discord engagement response(s) via {provider}/{model}.", "operator");
        await _db.SaveChangesAsync(ct);
        return (generated, $"Generated {generated} response draft(s). {syncMessage}");
    }

    /// <summary>Adds a pasted DM (received on the operator's own account) for drafting.</summary>
    public async Task<EngagementDraft> CreateManualEngagementAsync(
        string author, string sourceText, EngagementDraftKind kind, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var draft = new EngagementDraft
        {
            Platform = "discord", Kind = kind, ExternalId = Guid.NewGuid().ToString("N"),
            AuthorName = author.Trim(), SourceText = sourceText.Trim(), ReceivedAt = now,
            CreatedAt = now, UpdatedAt = now
        };
        _db.EngagementDrafts.Add(draft);
        await _db.SaveChangesAsync(ct);
        return draft;
    }

    /// <summary>Publishes a reviewed reply into the channel, quoting the original message.</summary>
    public async Task<(bool Succeeded, string Message)> PublishEngagementAsync(long draftId, CancellationToken ct)
    {
        var draft = await _db.EngagementDrafts.FirstOrDefaultAsync(d => d.Id == draftId, ct);
        if (draft is null) return (false, "Engagement draft not found.");
        if (draft.Status == EngagementDraftStatus.Published) return (false, "This response was already published.");
        if (string.IsNullOrWhiteSpace(draft.DraftText)) return (false, "Response text is empty.");
        if (string.IsNullOrWhiteSpace(draft.ThreadUrn) || string.IsNullOrWhiteSpace(draft.ParentCommentUrn))
            return (false, "This is a pasted message with no channel identity — copy the draft and send it as a DM in Discord.");
        if (draft.DraftText.Trim().Length > MaxMessageLength)
            return (false, $"Discord messages max out at {MaxMessageLength} characters.");

        var s = await GetSettingsAsync(tracking: false, ct);
        if (string.IsNullOrWhiteSpace(s.DiscordBotToken)) return (false, "Save the Discord bot token first.");
        if (s.GlobalKillSwitch) return (false, "Global kill switch is on; outbound responses are blocked.");

        var (ok, _, error) = await SendMessageAsync(
            s, draft.ThreadUrn, draft.DraftText.Trim(), replyToMessageId: draft.ParentCommentUrn, ct);
        if (!ok)
        {
            draft.Status = EngagementDraftStatus.Failed;
            draft.LastError = error;
            draft.UpdatedAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync(ct);
            return (false, "Discord reply failed: " + error);
        }

        draft.Status = EngagementDraftStatus.Published;
        draft.PublishedAt = DateTimeOffset.UtcNow;
        draft.UpdatedAt = DateTimeOffset.UtcNow;
        draft.LastError = "";
        await _db.SaveChangesAsync(ct);
        _audit.Record("EngagementDraft", draft.Id, "DiscordReplyPublished",
            "Published a reviewed reply in a Discord channel.", "operator");
        await _db.SaveChangesAsync(ct);
        return (true, "Reply posted in the channel.");
    }

    private async Task<(bool Ok, string MessageId, string Error)> SendMessageAsync(
        OperatorSettings s, string channelId, string content, string? replyToMessageId, CancellationToken ct)
    {
        object payload = replyToMessageId is null
            ? new { content }
            : new
            {
                content,
                // fail_if_not_exists=false posts a normal message if the original was deleted.
                message_reference = new { message_id = replyToMessageId, fail_if_not_exists = false }
            };
        var http = _httpFactory.CreateClient(ConnectorSupport.HttpClientName);
        using var request = BotRequest(HttpMethod.Post, $"/channels/{channelId}/messages", s);
        request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        using var response = await http.SendAsync(request, ct);
        var json = await response.Content.ReadAsStringAsync(ct);
        if (response.StatusCode == HttpStatusCode.Forbidden)
            return (false, "", "The bot lacks permission to post in that channel (needs View Channel + Send Messages).");
        if (!response.IsSuccessStatusCode)
            return (false, "", $"HTTP {(int)response.StatusCode}: {ApiError(json)}");
        return TryJson(json, out var root)
            ? (true, GetString(root, "id"), "")
            : (false, "", "Discord returned invalid JSON.");
    }

    private static HttpRequestMessage BotRequest(HttpMethod method, string path, OperatorSettings s)
    {
        var request = new HttpRequestMessage(method, ApiRoot + path);
        request.Headers.TryAddWithoutValidation("Authorization", "Bot " + s.DiscordBotToken.Trim());
        return request;
    }

    private async Task<OperatorSettings> GetSettingsAsync(bool tracking, CancellationToken ct)
    {
        var query = tracking ? _db.OperatorSettings.AsQueryable() : _db.OperatorSettings.AsNoTracking();
        return await query.FirstOrDefaultAsync(ct) ?? new OperatorSettings { Id = 1 };
    }

    private static Dictionary<long, string> ParseReplies(string output)
    {
        var start = output.IndexOf('{');
        var end = output.LastIndexOf('}');
        if (start < 0 || end <= start) throw new JsonException("no JSON object found");
        using var doc = JsonDocument.Parse(output[start..(end + 1)]);
        if (!doc.RootElement.TryGetProperty("replies", out var replies) || replies.ValueKind != JsonValueKind.Array)
            throw new JsonException("missing replies array");
        var result = new Dictionary<long, string>();
        foreach (var item in replies.EnumerateArray())
        {
            var id = GetInt64(item, "id", 0);
            var text = GetString(item, "text");
            if (id > 0 && text.Length > 0) result[id] = text;
        }
        return result;
    }

    private static bool TryJson(string json, out JsonElement root)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            root = doc.RootElement.Clone();
            return true;
        }
        catch { root = default; return false; }
    }

    private static string GetString(JsonElement root, string name) =>
        root.ValueKind == JsonValueKind.Object && root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? "" : "";

    private static long GetInt64(JsonElement root, string name, long fallback) =>
        root.ValueKind == JsonValueKind.Object && root.TryGetProperty(name, out var value) && value.TryGetInt64(out var number)
            ? number : fallback;

    private static string ApiError(string body)
    {
        if (TryJson(body, out var root))
        {
            var message = GetString(root, "message");
            if (message.Length > 0) return message.Length <= 500 ? message : message[..500];
        }
        var compact = string.Join(' ', (body ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return compact.Length switch { 0 => "no error details", <= 500 => compact, _ => compact[..500] };
    }
}
