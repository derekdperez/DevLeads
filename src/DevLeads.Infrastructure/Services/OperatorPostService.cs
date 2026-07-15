using System.Xml.Linq;
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
/// Syncs the operator's OWN reddit posts into "My posts" and tracks their reply counts
/// over time. Uses reddit's anonymous RSS endpoints with 6s pacing (the JSON API 403s
/// from this host and the per-IP RSS budget bursts 429 — see RedditConnector).
/// Other platforms (Upwork, Craigslist, Monster, Indeed…) are manual entries for now.
/// </summary>
public sealed class OperatorPostService
{
    private static readonly XNamespace Atom = "http://www.w3.org/2005/Atom";
    private static readonly TimeSpan RequestPacing = TimeSpan.FromSeconds(6);

    /// <summary>Hiring-shaped subreddits: posts here are resume/for-hire posts by definition.</summary>
    private static readonly string[] JobCommunities =
    {
        "forhire", "freelance_forhire", "dotnetjobs", "programmingjobs", "jobbit",
        "hireaprogrammer", "hireawebdeveloper", "remotejobs", "jobs", "slavelabour"
    };

    private readonly DevLeadsDbContext _db;
    private readonly IHttpClientFactory _httpFactory;
    private readonly AiTextRouter _text;
    private readonly DiscoveryActivityTracker _activity;
    private readonly AuditService _audit;
    private readonly ILogger<OperatorPostService> _log;

    public OperatorPostService(DevLeadsDbContext db, IHttpClientFactory httpFactory,
        AiTextRouter text, DiscoveryActivityTracker activity,
        AuditService audit, ILogger<OperatorPostService> log)
    {
        _db = db;
        _httpFactory = httpFactory;
        _text = text;
        _activity = activity;
        _audit = audit;
        _log = log;
    }

    /// <summary>
    /// One AI call drafts a platform-appropriate post (reddit/craigslist/linkedin/upwork/
    /// gmail template/discord offer) in the operator's real identity, using the best-performing tracked
    /// posts as voice reference. Saved as a Draft — the operator posts it manually and
    /// then marks it Active with its URL.
    /// </summary>
    public async Task<(OperatorPost? Post, string Message)> GeneratePostAsync(
        string platform, long? campaignId, string extraInstructions, CancellationToken ct)
    {
        platform = platform.Trim().ToLowerInvariant();
        if (!PlatformPostPrompts.SupportedPlatforms.Contains(platform))
            return (null, "Unsupported platform: " + platform);

        var settings = await _db.OperatorSettings.AsNoTracking().FirstOrDefaultAsync(ct) ?? new OperatorSettings();

        var skills = await _db.Skills.AsNoTracking().Where(s => s.Enabled).ToListAsync(ct);
        var objective = campaignId is { } cid
            ? (await _db.Campaigns.AsNoTracking().FirstOrDefaultAsync(c => c.Id == cid, ct))?.Objective ?? ""
            : "";
        var reference = (await _db.OperatorPosts.AsNoTracking()
                .Where(p => p.Body.Length > 200)
                .OrderByDescending(p => p.ReplyCount).ThenByDescending(p => p.PostedAt)
                .Take(2).ToListAsync(ct))
            .Select(p => (p.Title, p.Body, p.ReplyCount)).ToList();

        var prompt = PlatformPostPrompts.BuildPostPrompt(
            platform, settings, SkillMatcher.PromptSummary(skills), objective, reference, extraInstructions);

        _activity.RunStarted("my_posts_draft", $"Drafting a {platform} post");
        var timeout = TimeSpan.FromSeconds(Math.Clamp(settings.AiTimeoutSeconds * 2, 120, 600));
        var (ok, text, error, model) = await _text.GenerateTextAsync(AiFeature.PostDrafting, prompt, settings, timeout, ct);
        if (!ok)
        {
            _activity.RunCompleted("my_posts_draft", healthy: false, "Post draft failed: " + error);
            return (null, "Post generation failed: " + error);
        }

        var (title, body) = SplitTitle(text);
        var now = DateTimeOffset.UtcNow;
        var post = new OperatorPost
        {
            Platform = platform,
            ExternalId = Guid.NewGuid().ToString("N"),
            Title = title,
            Body = body,
            Status = OperatorPostStatus.Draft,
            CampaignId = campaignId,
            Notes = $"AI-drafted via {model} — review, post manually, then set the URL and mark Active.",
            PostedAt = now, CreatedAt = now, UpdatedAt = now
        };
        _db.OperatorPosts.Add(post);
        await _db.SaveChangesAsync(ct);
        _audit.Record("OperatorPost", post.Id, "PostDrafted", $"{platform} post drafted via {model}.");
        await _db.SaveChangesAsync(ct);
        _activity.RunCompleted("my_posts_draft", healthy: true, $"{platform} post drafted: {title}");
        return (post, $"{platform} draft ready: {title}");
    }

    private static (string Title, string Body) SplitTitle(string text)
    {
        var lines = text.Replace("\r\n", "\n").Split('\n');
        var first = Array.FindIndex(lines, l => !string.IsNullOrWhiteSpace(l));
        if (first >= 0 && lines[first].TrimStart().StartsWith('#'))
            return (lines[first].TrimStart('#', ' ').Trim(), string.Join('\n', lines[(first + 1)..]).Trim());
        return (lines.FirstOrDefault(l => !string.IsNullOrWhiteSpace(l))?.Trim() ?? "Draft post", text.Trim());
    }

    /// <summary>
    /// Imports the account's submitted posts and refreshes reply counts on tracked ones.
    /// <paramref name="jobPostsOnly"/> keeps personal posts out (default): a post imports
    /// when its subreddit is hiring-shaped or its title carries for-hire language.
    /// </summary>
    public async Task<(int Imported, int Refreshed, string Message)> SyncRedditAsync(
        bool jobPostsOnly, CancellationToken ct)
    {
        var settings = await _db.OperatorSettings.AsNoTracking().FirstOrDefaultAsync(ct) ?? new OperatorSettings();
        var username = settings.RedditUsername.Trim().TrimStart('u', '/').Trim('/');
        if (string.IsNullOrWhiteSpace(username))
            return (0, 0, "No reddit username configured (Settings → RedditUsername).");

        _activity.RunStarted("my_posts", $"Syncing u/{username}'s posts");
        try
        {
            // Local first, so bodies heal even when reddit throttles the network calls.
            await BackfillCrossPostBodiesAsync(ct);

            // Authenticated API when credentials exist: the submitted listing supplies
            // post identity and public stats; /api/info is queried in one batch for the
            // most complete per-post representation Reddit makes available.
            if (HasApiCredentials(settings))
            {
                var (imported, refreshed, viewsReported) = await SyncViaApiAsync(settings, username, jobPostsOnly, ct);
                var viewMessage = viewsReported > 0
                    ? $" View counts were reported for {viewsReported} post(s)."
                    : " Reddit did not expose view counts for these posts; enter them manually from Post Insights.";
                var apiMessage = $"u/{username} (API): {imported} imported, {refreshed} post(s) updated with live stats.{viewMessage}";
                _activity.RunCompleted("my_posts", healthy: true, apiMessage);
                return (imported, refreshed, apiMessage);
            }

            var http = _httpFactory.CreateClient(ConnectorSupport.HttpClientName);
            var importedRss = await ImportSubmittedAsync(http, username, jobPostsOnly, ct);
            var refreshedRss = await RefreshRepliesAsync(http, ct);
            var message = $"u/{username} (RSS fallback — add Reddit API credentials in Settings for views/upvotes/removal state): " +
                          $"{importedRss} imported, {refreshedRss} refreshed.";
            _activity.RunCompleted("my_posts", healthy: true, message);
            return (importedRss, refreshedRss, message);
        }
        catch (OperationCanceledException)
        {
            _activity.RunCompleted("my_posts", healthy: false, "My-posts sync cancelled.");
            throw;
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "My-posts reddit sync failed");
            _activity.RunCompleted("my_posts", healthy: false, "My-posts sync failed: " + ex.Message);
            return (0, 0, "Sync failed: " + ex.Message);
        }
    }

    private async Task<int> ImportSubmittedAsync(HttpClient http, string username, bool jobPostsOnly, CancellationToken ct)
    {
        var url = $"https://www.reddit.com/user/{Uri.EscapeDataString(username)}/submitted.rss?limit=100";
        using var resp = await http.GetAsync(url, ct);
        if ((int)resp.StatusCode is 429 or 403)
            throw new InvalidOperationException($"Reddit rate-limited the sync (HTTP {(int)resp.StatusCode}) — try again in a few minutes.");
        resp.EnsureSuccessStatusCode();

        var root = XDocument.Parse(await resp.Content.ReadAsStringAsync(ct)).Root;
        // Reddit's silent throttle returns HTTP 200 with an EMPTY feed ("Fetched 0" is a
        // red flag, not a success) — surface it so the operator retries instead of
        // trusting a hollow sync.
        if (root is null || !root.Elements(Atom + "entry").Any())
            throw new InvalidOperationException("Reddit returned an empty feed — likely silently throttled. Try again in a few minutes.");

        var imported = 0;
        var now = DateTimeOffset.UtcNow;
        foreach (var entry in root.Elements(Atom + "entry"))
        {
            var externalId = ((string?)entry.Element(Atom + "id") ?? "").Replace("t3_", "");
            var title = System.Net.WebUtility.HtmlDecode((string?)entry.Element(Atom + "title") ?? "");
            var link = (string?)entry.Element(Atom + "link")?.Attribute("href") ?? "";
            var community = (string?)entry.Element(Atom + "category")?.Attribute("term") ?? "";
            var body = StripBoilerplate(StripHtml((string?)entry.Element(Atom + "content") ?? ""));
            var postedRaw = (string?)entry.Element(Atom + "published") ?? (string?)entry.Element(Atom + "updated");
            var posted = DateTimeOffset.TryParse(postedRaw, out var p) ? p : now;

            if (externalId.Length == 0 || title.Length == 0 || link.Length == 0) continue;
            // The feed includes comments the account made; only keep actual posts.
            if (!link.Contains("/comments/", StringComparison.OrdinalIgnoreCase)) continue;
            if (jobPostsOnly && !IsJobPost(title, community)) continue;

            if (await _db.OperatorPosts.AnyAsync(x => x.Platform == "reddit" && x.ExternalId == externalId, ct))
                continue;

            _db.OperatorPosts.Add(new OperatorPost
            {
                Platform = "reddit",
                ExternalId = externalId,
                Url = link,
                Title = title,
                Body = body,
                Community = community,
                Status = OperatorPostStatus.Active,
                PostedAt = posted,
                CreatedAt = now,
                UpdatedAt = now
            });
            imported++;
        }

        if (imported > 0)
        {
            _audit.Record("OperatorPost", 0, "RedditImported", $"Imported {imported} post(s) from u/{username}.");
            await _db.SaveChangesAsync(ct);
        }
        return imported;
    }

    public static bool HasApiCredentials(OperatorSettings s) =>
        !string.IsNullOrWhiteSpace(s.RedditClientId) &&
        !string.IsNullOrWhiteSpace(s.RedditClientSecret) &&
        !string.IsNullOrWhiteSpace(s.RedditAppPassword);

    // Token cache: script-app tokens last ~1h; refetching per sync would waste quota.
    private static string? _cachedToken;
    private static DateTimeOffset _tokenExpiresAt = DateTimeOffset.MinValue;
    private static readonly SemaphoreSlim TokenLock = new(1, 1);

    private async Task<string> GetAccessTokenAsync(OperatorSettings settings, string username, CancellationToken ct)
    {
        await TokenLock.WaitAsync(ct);
        try
        {
            if (_cachedToken is not null && DateTimeOffset.UtcNow < _tokenExpiresAt.AddMinutes(-5))
                return _cachedToken;

            var http = _httpFactory.CreateClient(ConnectorSupport.HttpClientName);
            using var request = new HttpRequestMessage(HttpMethod.Post, "https://www.reddit.com/api/v1/access_token")
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"] = "password",
                    ["username"] = username,
                    ["password"] = settings.RedditAppPassword
                })
            };
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
                "Basic", Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(
                    $"{settings.RedditClientId.Trim()}:{settings.RedditClientSecret.Trim()}")));

            using var resp = await http.SendAsync(request, ct);
            var json = await resp.Content.ReadAsStringAsync(ct);
            if (!resp.IsSuccessStatusCode)
                throw new InvalidOperationException($"Reddit OAuth failed (HTTP {(int)resp.StatusCode}): check the script-app credentials in Settings.");

            using var doc = System.Text.Json.JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("access_token", out var tokenEl))
                throw new InvalidOperationException("Reddit OAuth response had no access_token — check the script-app credentials (and that the account has no unsupported 2FA).");
            var expires = doc.RootElement.TryGetProperty("expires_in", out var e) ? e.GetInt32() : 3600;
            _cachedToken = tokenEl.GetString();
            _tokenExpiresAt = DateTimeOffset.UtcNow.AddSeconds(expires);
            return _cachedToken!;
        }
        finally { TokenLock.Release(); }
    }

    /// <summary>
    /// Authenticated sync combines the submitted listing with one batched /api/info
    /// request. Reddit may still omit view_count; ViewCountKnown records that distinction.
    /// </summary>
    private async Task<(int Imported, int Refreshed, int ViewsReported)> SyncViaApiAsync(
        OperatorSettings settings, string username, bool jobPostsOnly, CancellationToken ct)
    {
        var token = await GetAccessTokenAsync(settings, username, ct);
        var http = _httpFactory.CreateClient(ConnectorSupport.HttpClientName);
        using var request = new HttpRequestMessage(HttpMethod.Get,
            $"https://oauth.reddit.com/user/{Uri.EscapeDataString(username)}/submitted?limit=100&raw_json=1");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        using var resp = await http.SendAsync(request, ct);
        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException($"Reddit API listing failed (HTTP {(int)resp.StatusCode}).");

        using var doc = System.Text.Json.JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
        var submitted = doc.RootElement.GetProperty("data").GetProperty("children")
            .EnumerateArray()
            .Select(child => child.GetProperty("data").Clone())
            .ToList();
        var detailById = await GetPostDetailsAsync(http, token,
            submitted.Select(d => GetString(d, "name")).Where(name => name.Length > 0), ct);

        var tracked = await _db.OperatorPosts.Where(p => p.Platform == "reddit").ToListAsync(ct);
        var byId = tracked.ToDictionary(p => p.ExternalId, StringComparer.OrdinalIgnoreCase);
        var now = DateTimeOffset.UtcNow;
        int imported = 0, refreshed = 0, viewsReported = 0;

        foreach (var submittedData in submitted)
        {
            var id = GetString(submittedData, "id");
            var hasDetail = detailById.TryGetValue(id, out var detail);
            var d = hasDetail ? detail : submittedData;
            var title = d.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "";
            var community = d.TryGetProperty("subreddit", out var sr) ? sr.GetString() ?? "" : "";
            if (id.Length == 0) continue;

            byId.TryGetValue(id, out var post);
            if (post is null)
            {
                if (jobPostsOnly && !IsJobPost(title, community)) continue;
                post = new OperatorPost
                {
                    Platform = "reddit",
                    ExternalId = id,
                    Url = "https://www.reddit.com" + (d.TryGetProperty("permalink", out var pl) ? pl.GetString() : "/comments/" + id),
                    Title = title,
                    Community = community,
                    PostedAt = d.TryGetProperty("created_utc", out var cu)
                        ? DateTimeOffset.FromUnixTimeSeconds((long)cu.GetDouble()) : now,
                    CreatedAt = now
                };
                _db.OperatorPosts.Add(post);
                byId[id] = post;
                imported++;
            }

            var selftext = d.TryGetProperty("selftext", out var st) ? st.GetString() ?? "" : "";
            if (selftext.Length > post.Body.Length && selftext != "[removed]") post.Body = selftext;

            var upvotes = d.TryGetProperty("score", out var sc) ? sc.GetInt32() : post.UpvoteCount;
            var replies = d.TryGetProperty("num_comments", out var nc) ? nc.GetInt32() : post.ReplyCount;
            var hasViewCount = TryGetNonNegativeInt(d, "view_count", out var views);
            if (!hasViewCount && hasDetail)
                hasViewCount = TryGetNonNegativeInt(submittedData, "view_count", out views);
            if (!hasViewCount) views = post.ViewCount;

            // removed_by_category: "reddit" = automated filters, "moderator", "deleted"…
            var removedBy = d.TryGetProperty("removed_by_category", out var rb) && rb.ValueKind == System.Text.Json.JsonValueKind.String
                ? rb.GetString() : null;
            if (removedBy is not null)
            {
                if (post.Status is OperatorPostStatus.Active or OperatorPostStatus.Draft)
                    post.Notes = $"Removed by {removedBy} (detected {now.LocalDateTime:MMM d HH:mm}). " + post.Notes;
                post.Status = OperatorPostStatus.Removed;
            }
            else if (post.Status == OperatorPostStatus.Removed)
            {
                post.Status = OperatorPostStatus.Active; // reinstated on the platform
            }

            if (upvotes != post.UpvoteCount || replies != post.ReplyCount || views != post.ViewCount)
            {
                _db.OperatorPostSnapshots.Add(new OperatorPostSnapshot
                {
                    OperatorPostId = post.Id, // 0 for new posts — fixed up by EF via navigation
                    Post = post,
                    At = now,
                    ReplyCount = replies,
                    UpvoteCount = upvotes,
                    ViewCount = views
                });
            }
            post.UpvoteCount = upvotes;
            post.ReplyCount = replies;
            post.ViewCount = views;
            if (hasViewCount)
            {
                post.ViewCountKnown = true;
                viewsReported++;
            }
            post.LastCheckedAt = now;
            post.UpdatedAt = now;
            refreshed++;
        }

        await _db.SaveChangesAsync(ct);
        if (imported > 0)
        {
            _audit.Record("OperatorPost", 0, "RedditImported", $"Imported {imported} post(s) from u/{username} via API.");
            await _db.SaveChangesAsync(ct);
        }
        return (imported, refreshed, viewsReported);
    }

    /// <summary>Gets the fullest available representation for up to 100 posts in one official API call.</summary>
    private async Task<Dictionary<string, System.Text.Json.JsonElement>> GetPostDetailsAsync(
        HttpClient http, string token, IEnumerable<string> fullnames, CancellationToken ct)
    {
        var names = fullnames.Distinct(StringComparer.OrdinalIgnoreCase).Take(100).ToList();
        if (names.Count == 0) return new(StringComparer.OrdinalIgnoreCase);

        using var request = new HttpRequestMessage(HttpMethod.Get,
            "https://oauth.reddit.com/api/info?id=" + Uri.EscapeDataString(string.Join(',', names)) + "&raw_json=1");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        using var response = await http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            _log.LogWarning("Reddit /api/info returned HTTP {Status}; using submitted-listing stats", (int)response.StatusCode);
            return new(StringComparer.OrdinalIgnoreCase);
        }

        using var doc = System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        return doc.RootElement.GetProperty("data").GetProperty("children").EnumerateArray()
            .Select(child => child.GetProperty("data"))
            .Where(data => GetString(data, "id").Length > 0)
            .ToDictionary(data => GetString(data, "id"), data => data.Clone(), StringComparer.OrdinalIgnoreCase);
    }

    private static bool TryGetNonNegativeInt(System.Text.Json.JsonElement data, string name, out int value)
    {
        if (data.TryGetProperty(name, out var element) && element.ValueKind == System.Text.Json.JsonValueKind.Number &&
            element.TryGetInt32(out value) && value >= 0)
            return true;
        value = 0;
        return false;
    }

    /// <summary>
    /// Syncs the account's reddit inbox — DMs plus comment/post replies — into tracked
    /// messages. Prefers the authenticated API (full listing JSON with unread flags);
    /// otherwise uses the private inbox feed token from reddit.com/prefs/feeds (Settings)
    /// in its .rss form — the .json form 403s from this host just like the public JSON
    /// API. One request per sync; new items arrive Unread and local read state is never
    /// overwritten by a re-sync.
    /// </summary>
    public async Task<(int Imported, string Message)> SyncRedditInboxAsync(CancellationToken ct)
    {
        var settings = await _db.OperatorSettings.AsNoTracking().FirstOrDefaultAsync(ct) ?? new OperatorSettings();
        var username = settings.RedditUsername.Trim().TrimStart('u', '/').Trim('/');
        if (string.IsNullOrWhiteSpace(username))
            return (0, "No reddit username configured (Settings → RedditUsername).");

        string url;
        string? bearer = null;
        if (HasApiCredentials(settings))
        {
            bearer = await GetAccessTokenAsync(settings, username, ct);
            url = "https://oauth.reddit.com/message/inbox?limit=100&raw_json=1";
        }
        else if (!string.IsNullOrWhiteSpace(settings.RedditInboxFeedToken))
        {
            url = $"https://www.reddit.com/message/inbox/.rss?feed={Uri.EscapeDataString(settings.RedditInboxFeedToken.Trim())}" +
                  $"&user={Uri.EscapeDataString(username)}&limit=100";
        }
        else
        {
            return (0, "Inbox sync needs reddit API credentials or the private inbox feed token from reddit.com/prefs/feeds (Settings).");
        }

        _activity.RunStarted("my_messages", $"Syncing u/{username}'s inbox");
        try
        {
            var http = _httpFactory.CreateClient(ConnectorSupport.HttpClientName);
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            if (bearer is not null)
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", bearer);
            using var resp = await http.SendAsync(request, ct);
            if ((int)resp.StatusCode is 429 or 403)
                throw new InvalidOperationException($"Reddit rate-limited the inbox sync (HTTP {(int)resp.StatusCode}) — try again in a few minutes.");
            resp.EnsureSuccessStatusCode();

            var payload = await resp.Content.ReadAsStringAsync(ct);
            var imported = bearer is not null
                ? await UpsertInboxListingAsync(payload, ct)
                : await UpsertInboxFeedAsync(payload, ct);
            var message = $"u/{username} inbox ({(bearer is null ? "feed" : "API")}): {imported} new message(s).";
            _activity.RunCompleted("my_messages", healthy: true, message);
            return (imported, message);
        }
        catch (OperationCanceledException)
        {
            _activity.RunCompleted("my_messages", healthy: false, "Inbox sync cancelled.");
            throw;
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Reddit inbox sync failed");
            _activity.RunCompleted("my_messages", healthy: false, "Inbox sync failed: " + ex.Message);
            return (0, "Inbox sync failed: " + ex.Message);
        }
    }

    /// <summary>
    /// Marks a message read locally as soon as it is opened. For Reddit messages, also
    /// makes a best-effort official /api/read_message call when OAuth credentials exist.
    /// A remote failure never leaves an item unread after the operator read it here.
    /// </summary>
    public async Task<bool> MarkMessageReadAsync(long messageId, CancellationToken ct)
    {
        var message = await _db.OperatorMessages.FirstOrDefaultAsync(m => m.Id == messageId, ct);
        if (message is null) return false;

        if (message.Status == OperatorMessageStatus.Unread)
        {
            message.Status = OperatorMessageStatus.Read;
            message.UpdatedAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync(ct);
        }

        if (!message.Platform.Equals("reddit", StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(message.ExternalId))
            return true;

        var settings = await _db.OperatorSettings.AsNoTracking().FirstOrDefaultAsync(ct) ?? new OperatorSettings();
        var username = settings.RedditUsername.Trim().TrimStart('u', '/').Trim('/');
        if (!HasApiCredentials(settings) || username.Length == 0) return true;

        try
        {
            var token = await GetAccessTokenAsync(settings, username, ct);
            var http = _httpFactory.CreateClient(ConnectorSupport.HttpClientName);
            using var request = new HttpRequestMessage(HttpMethod.Post, "https://oauth.reddit.com/api/read_message")
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["id"] = message.ExternalId
                })
            };
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            using var response = await http.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
                _log.LogWarning("Reddit read_message returned HTTP {Status} for {ExternalId}",
                    (int)response.StatusCode, message.ExternalId);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            _log.LogWarning("Reddit read_message timed out for {ExternalId}", message.ExternalId);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Could not mark Reddit message {ExternalId} read upstream", message.ExternalId);
        }
        return true;
    }

    private async Task<int> UpsertInboxListingAsync(string json, CancellationToken ct)
    {
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        var children = doc.RootElement.GetProperty("data").GetProperty("children");

        var known = (await _db.OperatorMessages.AsNoTracking()
                .Where(m => m.Platform == "reddit").Select(m => m.ExternalId).ToListAsync(ct))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var trackedPosts = await _db.OperatorPosts.AsNoTracking()
            .Where(p => p.Platform == "reddit")
            .Select(p => new { p.Id, p.ExternalId }).ToListAsync(ct);

        var now = DateTimeOffset.UtcNow;
        var imported = 0;
        foreach (var child in children.EnumerateArray())
        {
            var kindTag = child.TryGetProperty("kind", out var k) ? k.GetString() ?? "" : "";
            if (!child.TryGetProperty("data", out var d)) continue;
            var externalId = GetString(d, "name");
            if (externalId.Length == 0 || known.Contains(externalId)) continue;

            var subject = GetString(d, "subject");
            var wasComment = d.TryGetProperty("was_comment", out var wc) &&
                             wc.ValueKind == System.Text.Json.JsonValueKind.True;
            var kind = kindTag == "t4" && !wasComment ? OperatorMessageKind.PrivateMessage
                : subject.Contains("mention", StringComparison.OrdinalIgnoreCase) ? OperatorMessageKind.Mention
                : subject.Contains("post reply", StringComparison.OrdinalIgnoreCase) ? OperatorMessageKind.PostReply
                : wasComment || kindTag == "t1" ? OperatorMessageKind.CommentReply
                : OperatorMessageKind.Other;

            // Reply subjects are generic ("post reply"); the post title is the useful one.
            var linkTitle = GetString(d, "link_title");
            if (kind != OperatorMessageKind.PrivateMessage && linkTitle.Length > 0)
                subject = linkTitle;

            var context = GetString(d, "context");
            var messageUrl = context.Length > 0
                ? "https://www.reddit.com" + context
                : "https://www.reddit.com/message/messages/" + GetString(d, "id");

            // Tie a reply back to the tracked post it landed on.
            long? postId = null;
            var match = System.Text.RegularExpressions.Regex.Match(context, @"/comments/([a-z0-9]+)",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (match.Success)
                postId = trackedPosts.FirstOrDefault(p =>
                    p.ExternalId.Equals(match.Groups[1].Value, StringComparison.OrdinalIgnoreCase))?.Id;

            var received = d.TryGetProperty("created_utc", out var cu) && cu.ValueKind == System.Text.Json.JsonValueKind.Number
                ? DateTimeOffset.FromUnixTimeSeconds((long)cu.GetDouble()) : now;
            var isNew = d.TryGetProperty("new", out var nw) && nw.ValueKind == System.Text.Json.JsonValueKind.True;

            _db.OperatorMessages.Add(new OperatorMessage
            {
                Platform = "reddit",
                ExternalId = externalId,
                Kind = kind,
                Author = GetString(d, "author"),
                Subject = subject,
                Body = GetString(d, "body"),
                Community = GetString(d, "subreddit"),
                Url = messageUrl,
                Status = isNew ? OperatorMessageStatus.Unread : OperatorMessageStatus.Read,
                OperatorPostId = postId,
                ReceivedAt = received,
                CreatedAt = now,
                UpdatedAt = now
            });
            known.Add(externalId);
            imported++;
        }

        if (imported > 0)
        {
            await _db.SaveChangesAsync(ct);
            _audit.Record("OperatorMessage", 0, "InboxSynced", $"Imported {imported} inbox message(s) from reddit.");
            await _db.SaveChangesAsync(ct);
        }
        return imported;
    }

    private static string GetString(System.Text.Json.JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == System.Text.Json.JsonValueKind.String
            ? v.GetString() ?? "" : "";

    /// <summary>
    /// Anonymous fallback: the private inbox feed as Atom. Entry ids are fullnames
    /// (t4_/t1_), titles read "from X via sub sent N ago: post reply" for replies (the
    /// DM subject for t4s), and the feed carries no unread flag — so anything received
    /// in the last 7 days imports as Unread and older backfill as Read.
    /// </summary>
    private async Task<int> UpsertInboxFeedAsync(string xml, CancellationToken ct)
    {
        var root = XDocument.Parse(xml).Root;
        var entries = root?.Elements(Atom + "entry").ToList() ?? new List<XElement>();

        var known = (await _db.OperatorMessages.AsNoTracking()
                .Where(m => m.Platform == "reddit").Select(m => m.ExternalId).ToListAsync(ct))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var trackedPosts = await _db.OperatorPosts.AsNoTracking()
            .Where(p => p.Platform == "reddit")
            .Select(p => new { p.Id, p.ExternalId }).ToListAsync(ct);

        var now = DateTimeOffset.UtcNow;
        var unreadCutoff = now.AddDays(-7);
        var imported = 0;
        foreach (var entry in entries)
        {
            var externalId = (string?)entry.Element(Atom + "id") ?? "";
            if (externalId.Length == 0 || known.Contains(externalId)) continue;

            var title = System.Net.WebUtility.HtmlDecode((string?)entry.Element(Atom + "title") ?? "");
            var author = System.Text.RegularExpressions.Regex.Replace(
                (string?)entry.Element(Atom + "author")?.Element(Atom + "name") ?? "", "^/?u/", "");
            var community = (string?)entry.Element(Atom + "category")?.Attribute("term") ?? "";
            var link = (string?)entry.Element(Atom + "link")?.Attribute("href") ?? "";
            var received = DateTimeOffset.TryParse((string?)entry.Element(Atom + "updated"), out var u) ? u : now;
            var body = ExtractFeedBody((string?)entry.Element(Atom + "content") ?? "");

            var isPm = externalId.StartsWith("t4", StringComparison.OrdinalIgnoreCase);
            var subject = ParseFeedSubject(title);
            var kind = isPm ? OperatorMessageKind.PrivateMessage
                : subject.Contains("mention", StringComparison.OrdinalIgnoreCase) ? OperatorMessageKind.Mention
                : subject.Equals("post reply", StringComparison.OrdinalIgnoreCase) ? OperatorMessageKind.PostReply
                : OperatorMessageKind.CommentReply;

            long? postId = null;
            var match = System.Text.RegularExpressions.Regex.Match(link, @"/comments/([a-z0-9]+)(?:/([^/?#]+))?",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (match.Success)
            {
                postId = trackedPosts.FirstOrDefault(p =>
                    p.ExternalId.Equals(match.Groups[1].Value, StringComparison.OrdinalIgnoreCase))?.Id;
                // Reply subjects are generic ("post reply"); the thread slug names the thread.
                if (!isPm && match.Groups[2].Success)
                    subject = match.Groups[2].Value.Replace('_', ' ');
            }

            _db.OperatorMessages.Add(new OperatorMessage
            {
                Platform = "reddit",
                ExternalId = externalId,
                Kind = kind,
                Author = author,
                Subject = subject,
                Body = body,
                Community = community,
                Url = link,
                Status = received > unreadCutoff ? OperatorMessageStatus.Unread : OperatorMessageStatus.Read,
                OperatorPostId = postId,
                ReceivedAt = received,
                CreatedAt = now,
                UpdatedAt = now
            });
            known.Add(externalId);
            imported++;
        }

        if (imported > 0)
        {
            await _db.SaveChangesAsync(ct);
            _audit.Record("OperatorMessage", 0, "InboxSynced", $"Imported {imported} inbox message(s) from reddit (feed).");
            await _db.SaveChangesAsync(ct);
        }
        return imported;
    }

    /// <summary>Drops the "from X via sub sent N ago:" feed prefix, keeping the subject/label.</summary>
    private static string ParseFeedSubject(string title)
    {
        var m = System.Text.RegularExpressions.Regex.Match(
            title, @"^from\s+\S+(?:\s+via\s+\S+)?\s+sent\s+.+?\s+ago:\s*(.+)$",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        return (m.Success ? m.Groups[1].Value : title).Trim();
    }

    /// <summary>Keeps only the message markdown (between reddit's SC_OFF/SC_ON markers), dropping the header line.</summary>
    private static string ExtractFeedBody(string html)
    {
        var start = html.IndexOf("<!-- SC_OFF -->", StringComparison.Ordinal);
        if (start >= 0)
        {
            var end = html.IndexOf("<!-- SC_ON -->", start, StringComparison.Ordinal);
            html = end > start ? html[start..end] : html[start..];
        }
        return StripHtml(html);
    }

    /// <summary>
    /// A cross-posted ad gets its full selftext in only ONE feed entry ("submitted by"
    /// stubs elsewhere). The copies share a title, so the longest same-title body is the
    /// authoritative text — pure local repair, no network.
    /// </summary>
    private async Task BackfillCrossPostBodiesAsync(CancellationToken ct)
    {
        var redditPosts = await _db.OperatorPosts.Where(p => p.Platform == "reddit").ToListAsync(ct);
        foreach (var group in redditPosts.GroupBy(p => p.Title, StringComparer.OrdinalIgnoreCase))
        {
            var best = group.OrderByDescending(p => p.Body.Length).First();
            if (best.Body.Length < 200) continue; // nothing authoritative in this group
            foreach (var stub in group.Where(p => p.Body.Length < best.Body.Length / 2))
            {
                stub.Body = best.Body;
                stub.UpdatedAt = DateTimeOffset.UtcNow;
            }
        }
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Pulls the post plus every visible reply, then one AI call summarizes the thread's
    /// main points and suggests how to move forward as the original poster. Non-reddit
    /// posts (no fetchable thread) are summarized from the stored text and notes.
    /// </summary>
    public async Task<(bool Ok, string Message)> SummarizeThreadAsync(long postId, CancellationToken ct)
    {
        var post = await _db.OperatorPosts.FirstOrDefaultAsync(p => p.Id == postId, ct);
        if (post is null) return (false, "Post not found.");

        var settings = await _db.OperatorSettings.AsNoTracking().FirstOrDefaultAsync(ct) ?? new OperatorSettings();
        if (_text.ProviderFor(settings, AiFeature.ThreadSummary).Equals("Heuristic", StringComparison.OrdinalIgnoreCase))
            return (false, "Thread summarization is set to Heuristic — choose OpenCode or Codex in Settings.");

        var replies = new List<string>();
        if (post.Platform == "reddit" && post.Url.Contains("/comments/", StringComparison.OrdinalIgnoreCase))
        {
            var http = _httpFactory.CreateClient(ConnectorSupport.HttpClientName);
            using var resp = await http.GetAsync(post.Url.TrimEnd('/') + ".rss?limit=100", ct);
            if ((int)resp.StatusCode is 429 or 403)
                return (false, $"Reddit rate-limited the thread fetch (HTTP {(int)resp.StatusCode}) — try again in a few minutes.");
            if (resp.IsSuccessStatusCode)
            {
                var root = XDocument.Parse(await resp.Content.ReadAsStringAsync(ct)).Root;
                var entries = root?.Elements(Atom + "entry").ToList() ?? new List<XElement>();
                var fullBody = StripBoilerplate(StripHtml((string?)entries.FirstOrDefault()?.Element(Atom + "content") ?? ""));
                if (fullBody.Length > post.Body.Length) post.Body = fullBody;
                foreach (var entry in entries.Skip(1))
                {
                    var author = (string?)entry.Element(Atom + "author")?.Element(Atom + "name") ?? "someone";
                    var replyText = StripHtml((string?)entry.Element(Atom + "content") ?? "");
                    if (replyText.Length > 0) replies.Add($"{author}: {Truncate(replyText, 700)}");
                }
                post.ReplyCount = replies.Count;
                post.LastCheckedAt = DateTimeOffset.UtcNow;
            }
        }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"You advise {settings.OperatorName}, a consultant who posted the following on {post.Platform} ({(string.IsNullOrWhiteSpace(post.Community) ? "n/a" : post.Community)}).");
        sb.AppendLine();
        sb.AppendLine("ORIGINAL POST — " + post.Title);
        sb.AppendLine(Truncate(post.Body, 3000));
        sb.AppendLine();
        if (replies.Count > 0)
        {
            sb.AppendLine($"REPLIES ({replies.Count}):");
            foreach (var r in replies.Take(40)) sb.AppendLine("- " + r);
        }
        else
        {
            sb.AppendLine("REPLIES: none visible yet.");
            if (!string.IsNullOrWhiteSpace(post.Notes)) sb.AppendLine("Operator notes: " + Truncate(post.Notes, 500));
        }
        sb.AppendLine();
        sb.AppendLine("Write plain text (no markdown fences), two short sections:");
        sb.AppendLine("MAIN POINTS: 3-6 tight bullets summarizing what the thread actually says (who replied, what they want, objections, signals of real interest vs noise).");
        sb.AppendLine("MOVE FORWARD: concrete advice for the original poster — who to answer first and roughly what to say, whether/how to edit or repost the ad, and the single next action. Ground everything in the thread; do not invent replies or interest that isn't there.");

        _activity.RunStarted("my_posts_summary", $"Summarizing thread: {post.Title}");
        var timeout = TimeSpan.FromSeconds(Math.Clamp(settings.AiTimeoutSeconds * 2, 120, 600));
        var (ok, text, error, model) = await _text.GenerateTextAsync(AiFeature.ThreadSummary, sb.ToString(), settings, timeout, ct);
        if (!ok)
        {
            _activity.RunCompleted("my_posts_summary", healthy: false, "Thread summary failed: " + error);
            await _db.SaveChangesAsync(ct); // keep any body/reply refresh we got
            return (false, "Summary failed: " + error);
        }

        post.ThreadSummary = text.Trim();
        post.SummarizedAt = DateTimeOffset.UtcNow;
        post.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        _audit.Record("OperatorPost", post.Id, "ThreadSummarized", $"Thread summarized via {model}.");
        await _db.SaveChangesAsync(ct);
        _activity.RunCompleted("my_posts_summary", healthy: true, $"Thread summarized: {post.Title}");
        return (true, "Thread summarized.");
    }

    /// <summary>
    /// One batched AI call rewrites each selected post with a DISTINCT strategy and
    /// saves them as Proposed revisions (nothing goes live: the operator applies each
    /// one on the platform, which freezes its before/after baseline). Leave at least
    /// one near-identical post unselected as the experiment's control. The provider/model
    /// come from the PostOptimization feature setting — rewrites need a stronger writer
    /// than the triage default, and a stable model keeps variants comparable across runs.
    /// </summary>
    public async Task<(int Created, string Message)> OptimizePostsAsync(
        IReadOnlyList<long> postIds, string extraInstructions, CancellationToken ct)
    {
        if (postIds.Count == 0) return (0, "Select at least one post to rewrite.");

        var settings = await _db.OperatorSettings.AsNoTracking().FirstOrDefaultAsync(ct) ?? new OperatorSettings();
        var (optProvider, optModel) = settings.AiFor(AiFeature.PostOptimization);
        if (optProvider.Equals("Heuristic", StringComparison.OrdinalIgnoreCase))
            return (0, "Post optimization is set to Heuristic — choose OpenCode or Codex in Settings.");

        var posts = await _db.OperatorPosts.Where(p => postIds.Contains(p.Id)).ToListAsync(ct);
        if (posts.Count == 0) return (0, "No matching posts.");

        var now = DateTimeOffset.UtcNow;
        var skills = await _db.Skills.AsNoTracking().Where(s => s.Enabled).ToListAsync(ct);
        var prompt = PlatformPostPrompts.BuildOptimizationPrompt(
            settings, SkillMatcher.PromptSummary(skills),
            posts.Select(p => (p.Id, p.Community, p.Title, p.Body,
                Math.Max(1.0 / 24, (now - p.PostedAt).TotalDays), p.ViewCount, p.ReplyCount, p.UpvoteCount)).ToList(),
            extraInstructions);

        _activity.RunStarted("post_optimize", $"Rewriting {posts.Count} post(s) — {optProvider}/{optModel}");
        var timeout = TimeSpan.FromSeconds(Math.Clamp(settings.AiTimeoutSeconds * 3, 180, 600));
        var (ok, text, error, model) = await _text.GenerateTextAsync(AiFeature.PostOptimization, prompt, settings, timeout, ct);
        if (!ok)
        {
            _activity.RunCompleted("post_optimize", healthy: false, "Optimization failed: " + error);
            return (0, "Optimization failed: " + error);
        }

        var json = OpenCodeTriageProvider.ExtractJsonObject(text);
        if (json is null)
        {
            _activity.RunCompleted("post_optimize", healthy: false, "Optimization returned no JSON.");
            return (0, "The model returned no parseable JSON — try again.");
        }

        var created = 0;
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            foreach (var v in doc.RootElement.GetProperty("variants").EnumerateArray())
            {
                var postId = v.TryGetProperty("postId", out var pid) && pid.ValueKind == System.Text.Json.JsonValueKind.Number
                    ? pid.GetInt64() : 0;
                var post = posts.FirstOrDefault(p => p.Id == postId);
                var title = GetString(v, "title");
                var body = GetString(v, "body");
                if (post is null || title.Length == 0 || body.Length == 0) continue;

                _db.OperatorPostRevisions.Add(new OperatorPostRevision
                {
                    OperatorPostId = post.Id,
                    Approach = GetString(v, "approach"),
                    Rationale = GetString(v, "rationale"),
                    OldTitle = post.Title,
                    OldBody = post.Body,
                    NewTitle = title,
                    NewBody = body,
                    Status = OperatorPostRevisionStatus.Proposed,
                    Provider = optProvider,
                    Model = model,
                    CreatedAt = now
                });
                created++;
            }
        }
        catch (Exception ex) when (ex is System.Text.Json.JsonException or KeyNotFoundException or InvalidOperationException)
        {
            _activity.RunCompleted("post_optimize", healthy: false, "Optimization JSON malformed.");
            return (0, "The model's JSON didn't match the expected shape — try again. (" + ex.Message + ")");
        }

        await _db.SaveChangesAsync(ct);
        _audit.Record("OperatorPost", 0, "OptimizationProposed", $"{created} rewrite(s) proposed via {model}.");
        await _db.SaveChangesAsync(ct);
        _activity.RunCompleted("post_optimize", healthy: created > 0, $"{created} rewrite(s) proposed.");
        return (created, created > 0
            ? $"{created} rewrite(s) proposed — review each approach, post it on reddit, then hit Apply to start its experiment clock."
            : "The model returned no usable variants — try again.");
    }

    /// <summary>
    /// Marks a proposed rewrite as live: freezes the pre-change baseline (counts and
    /// views/day) and snapshots the change moment for the graph. A changed title cannot
    /// be edited on reddit, so it spawns a NEW linked tracked post (Draft, ready to
    /// delete+repost) and expires the original; body-only changes update the post in
    /// place — the operator edits the selftext on reddit.
    /// </summary>
    public async Task<(bool Ok, string Message)> ApplyRevisionAsync(long revisionId, CancellationToken ct)
    {
        var rev = await _db.OperatorPostRevisions.Include(r => r.Post)
            .FirstOrDefaultAsync(r => r.Id == revisionId, ct);
        if (rev?.Post is null) return (false, "Revision not found.");
        if (rev.Status != OperatorPostRevisionStatus.Proposed) return (false, $"Revision is already {rev.Status}.");

        var post = rev.Post;
        var now = DateTimeOffset.UtcNow;
        var liveDays = Math.Max(1.0 / 24, (now - post.PostedAt).TotalDays);

        rev.BaselineViewCount = post.ViewCount;
        rev.BaselineReplyCount = post.ReplyCount;
        rev.BaselineUpvoteCount = post.UpvoteCount;
        rev.BaselineViewsPerDay = post.ViewCount / liveDays;
        rev.BaselineRepliesPerDay = post.ReplyCount / liveDays;
        rev.Status = OperatorPostRevisionStatus.Applied;
        rev.AppliedAt = now;

        // Change marker on the timeline: the graph draws revision lines at AppliedAt,
        // and this point anchors the "before" side even if counts were stale.
        _db.OperatorPostSnapshots.Add(new OperatorPostSnapshot
        {
            OperatorPostId = post.Id, At = now,
            ReplyCount = post.ReplyCount, UpvoteCount = post.UpvoteCount, ViewCount = post.ViewCount
        });

        string message;
        var titleChanged = !string.Equals(post.Title.Trim(), rev.NewTitle.Trim(), StringComparison.OrdinalIgnoreCase);
        if (titleChanged)
        {
            var repost = new OperatorPost
            {
                Platform = post.Platform,
                ExternalId = Guid.NewGuid().ToString("N"),
                Title = rev.NewTitle,
                Body = rev.NewBody,
                Community = post.Community,
                CampaignId = post.CampaignId,
                Status = OperatorPostStatus.Draft,
                Notes = $"Optimization repost of \"{Truncate(post.Title, 60)}\" (revision #{rev.Id}: {rev.Approach}). Delete the old post, post this, set the URL, mark Active.",
                PostedAt = now, CreatedAt = now, UpdatedAt = now
            };
            _db.OperatorPosts.Add(repost);
            post.Status = OperatorPostStatus.Expired;
            post.UpdatedAt = now;
            await _db.SaveChangesAsync(ct);
            rev.ResultPostId = repost.Id;
            message = "Title changed → reddit needs a delete+repost: the rewrite is a new Draft post — delete the old one on reddit, post the new content, set its URL, mark it Active.";
        }
        else
        {
            post.Body = rev.NewBody;
            post.UpdatedAt = now;
            message = "Body updated — now edit the post's selftext on reddit to match. Views/replies from here on count against this revision.";
        }

        await _db.SaveChangesAsync(ct);
        _audit.Record("OperatorPost", post.Id, "RevisionApplied",
            $"Revision #{rev.Id} ({rev.Approach}) applied{(titleChanged ? $" as repost #{rev.ResultPostId}" : "")}. Baseline: {rev.BaselineViewCount} views ({rev.BaselineViewsPerDay:0.#}/day), {rev.BaselineReplyCount} replies.");
        await _db.SaveChangesAsync(ct);
        return (true, message);
    }

    private static string Truncate(string s, int max) =>
        string.IsNullOrEmpty(s) ? "" : s.Length <= max ? s : s[..max] + "…";

    private static bool IsJobPost(string title, string community) =>
        JobCommunities.Contains(community, StringComparer.OrdinalIgnoreCase) ||
        title.Contains("[for hire]", StringComparison.OrdinalIgnoreCase) ||
        title.Contains("for hire", StringComparison.OrdinalIgnoreCase) ||
        title.Contains("[hiring]", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Refreshes reply counts on tracked reddit posts (stalest first, paced at 6s per
    /// request, capped per run to protect the reddit RSS budget). Snapshots every change.
    /// </summary>
    private async Task<int> RefreshRepliesAsync(HttpClient http, CancellationToken ct)
    {
        var staleCutoff = DateTimeOffset.UtcNow.AddHours(-3);
        var due = await _db.OperatorPosts
            .Where(p => p.Platform == "reddit" && p.Status == OperatorPostStatus.Active &&
                        (p.LastCheckedAt == null || p.LastCheckedAt < staleCutoff))
            .OrderBy(p => p.LastCheckedAt)
            .Take(10)
            .ToListAsync(ct);

        var refreshed = 0;
        foreach (var post in due)
        {
            ct.ThrowIfCancellationRequested();
            await Task.Delay(RequestPacing, ct);

            using var resp = await http.GetAsync(post.Url.TrimEnd('/') + ".rss?limit=100", ct);
            if ((int)resp.StatusCode is 429 or 403)
            {
                _log.LogWarning("Reddit rate-limited reply refresh (HTTP {Status}) — stopping this run.", (int)resp.StatusCode);
                break;
            }
            if (!resp.IsSuccessStatusCode)
            {
                // 404 = post removed/deleted on the platform: record that, stop polling it.
                if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
                    post.Status = OperatorPostStatus.Removed;
                post.LastCheckedAt = DateTimeOffset.UtcNow;
                continue;
            }

            var root = XDocument.Parse(await resp.Content.ReadAsStringAsync(ct)).Root;
            // First entry is the post itself; the rest are replies (feed caps at 100).
            var entries = root?.Elements(Atom + "entry").ToList() ?? new List<XElement>();
            var replyCount = Math.Max(0, entries.Count - 1);

            // The user's submitted feed only carries full selftext for ONE copy of a
            // cross-posted ad ("submitted by…" stubs for the rest) — the post's own feed
            // always has it, so harvest the authoritative body while we're here anyway.
            var fullBody = StripBoilerplate(StripHtml((string?)entries.FirstOrDefault()?.Element(Atom + "content") ?? ""));
            // Filter/mod removals replace the anonymous view of the body with "[removed]".
            if (fullBody.Equals("[removed]", StringComparison.OrdinalIgnoreCase))
            {
                if (post.Status is OperatorPostStatus.Active or OperatorPostStatus.Draft)
                {
                    post.Status = OperatorPostStatus.Removed;
                    post.Notes = $"Shows as [removed] to anonymous viewers (detected {DateTimeOffset.UtcNow.LocalDateTime:MMM d HH:mm}). " + post.Notes;
                }
            }
            else if (fullBody.Length > post.Body.Length) post.Body = fullBody;
            if (replyCount != post.ReplyCount)
            {
                post.ReplyCount = replyCount;
                _db.OperatorPostSnapshots.Add(new OperatorPostSnapshot
                {
                    OperatorPostId = post.Id,
                    At = DateTimeOffset.UtcNow,
                    ReplyCount = replyCount
                });
            }
            post.LastCheckedAt = DateTimeOffset.UtcNow;
            post.UpdatedAt = DateTimeOffset.UtcNow;
            refreshed++;
        }

        await _db.SaveChangesAsync(ct);
        return refreshed;
    }

    private static string StripHtml(string html) =>
        System.Net.WebUtility.HtmlDecode(System.Text.RegularExpressions.Regex.Replace(html, "<.*?>", " ")).Trim();

    /// <summary>Removes reddit's "submitted by /u/… [link] [comments]" feed footer.</summary>
    private static string StripBoilerplate(string text) =>
        System.Text.RegularExpressions.Regex.Replace(
            text, @"\s*submitted by\s+/?u/\S+.*$", "",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Singleline).Trim();
}
