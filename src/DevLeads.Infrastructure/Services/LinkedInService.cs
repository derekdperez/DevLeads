using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
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
/// LinkedIn member OAuth, text-post publishing, scheduled publishing, comment monitoring,
/// and human-reviewed response drafts. Private LinkedIn inbox access is intentionally not
/// claimed: LinkedIn does not expose a general member-messaging API.
/// </summary>
public sealed class LinkedInService
{
    private const string AuthorizationEndpoint = "https://www.linkedin.com/oauth/v2/authorization";
    private const string TokenEndpoint = "https://www.linkedin.com/oauth/v2/accessToken";
    private const string UserInfoEndpoint = "https://api.linkedin.com/v2/userinfo";
    private const string ApiRoot = "https://api.linkedin.com/rest";

    private readonly DevLeadsDbContext _db;
    private readonly IHttpClientFactory _httpFactory;
    private readonly AiTextRouter _text;
    private readonly AuditService _audit;
    private readonly ILogger<LinkedInService> _log;

    public LinkedInService(DevLeadsDbContext db, IHttpClientFactory httpFactory,
        AiTextRouter text, AuditService audit, ILogger<LinkedInService> log)
    {
        _db = db;
        _httpFactory = httpFactory;
        _text = text;
        _audit = audit;
        _log = log;
    }

    public sealed record ConnectionStatus(
        bool Configured, bool Connected, bool TokenExpired, DateTimeOffset? ExpiresAt,
        string MemberName, string MemberId, string Scopes, bool CanReadEngagement);

    public async Task<ConnectionStatus> GetConnectionStatusAsync(CancellationToken ct)
    {
        var s = await GetSettingsAsync(tracking: false, ct);
        var configured = !string.IsNullOrWhiteSpace(s.LinkedInClientId) &&
                         !string.IsNullOrWhiteSpace(s.LinkedInClientSecret);
        var expired = s.LinkedInAccessTokenExpiresAt is { } expires && expires <= DateTimeOffset.UtcNow;
        var connected = !string.IsNullOrWhiteSpace(s.LinkedInAccessToken) &&
                        !string.IsNullOrWhiteSpace(s.LinkedInMemberId) && !expired;
        return new ConnectionStatus(configured, connected, expired, s.LinkedInAccessTokenExpiresAt,
            s.LinkedInMemberName, s.LinkedInMemberId, s.LinkedInScopes,
            HasScope(s.LinkedInScopes, "r_member_social") || HasScope(s.LinkedInScopes, "r_member_social_feed"));
    }

    /// <summary>Creates a state-protected three-legged OAuth authorization URL.</summary>
    public async Task<(string? Url, string Message)> CreateAuthorizationUrlAsync(
        string requestCallbackUrl, CancellationToken ct)
    {
        var s = await GetSettingsAsync(tracking: true, ct);
        if (string.IsNullOrWhiteSpace(s.LinkedInClientId) || string.IsNullOrWhiteSpace(s.LinkedInClientSecret))
            return (null, "Save the LinkedIn client id and client secret first.");

        var redirectUri = ResolveRedirectUri(s, requestCallbackUrl);
        if (!Uri.TryCreate(redirectUri, UriKind.Absolute, out var redirect))
            return (null, "LinkedIn redirect URI must be an absolute URL.");
        // LinkedIn delivers the authorization code to the redirect URI, so anything that
        // isn't this app's own callback (e.g. the developer portal's OAuth-tool URL) can
        // never complete the connection.
        if (!redirect.AbsolutePath.EndsWith("/api/linkedin/callback", StringComparison.OrdinalIgnoreCase))
            return (null, "The redirect URI must be this app's own /api/linkedin/callback endpoint — " +
                "LinkedIn sends the sign-in code there. Leave the field blank to use the default shown " +
                "above, and register that same URL in the LinkedIn Developer Portal.");

        var state = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        s.LinkedInOAuthState = state;
        s.LinkedInOAuthStateExpiresAt = DateTimeOffset.UtcNow.AddMinutes(15);
        await _db.SaveChangesAsync(ct);

        var scopes = NormalizeScopes(s.LinkedInScopes);
        var url = AuthorizationEndpoint + "?" + string.Join('&', new Dictionary<string, string>
        {
            ["response_type"] = "code",
            ["client_id"] = s.LinkedInClientId.Trim(),
            ["redirect_uri"] = redirectUri,
            ["state"] = state,
            ["scope"] = scopes
        }.Select(kv => $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}"));
        return (url, "Opening LinkedIn authorization.");
    }

    /// <summary>Validates OAuth state, exchanges the code, and resolves the member profile.</summary>
    public async Task<(bool Succeeded, string Message)> CompleteOAuthAsync(
        string code, string state, string requestCallbackUrl, CancellationToken ct)
    {
        var s = await GetSettingsAsync(tracking: true, ct);
        if (string.IsNullOrWhiteSpace(code)) return (false, "LinkedIn returned no authorization code.");
        if (string.IsNullOrWhiteSpace(state) || string.IsNullOrWhiteSpace(s.LinkedInOAuthState) ||
            s.LinkedInOAuthStateExpiresAt is null || s.LinkedInOAuthStateExpiresAt <= DateTimeOffset.UtcNow ||
            !FixedTimeEquals(state, s.LinkedInOAuthState))
            return (false, "LinkedIn OAuth state was missing, expired, or invalid. Start Connect again.");

        var redirectUri = ResolveRedirectUri(s, requestCallbackUrl);
        var http = _httpFactory.CreateClient(ConnectorSupport.HttpClientName);
        using var response = await http.PostAsync(TokenEndpoint, new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["code"] = code,
                ["client_id"] = s.LinkedInClientId.Trim(),
                ["client_secret"] = s.LinkedInClientSecret,
                ["redirect_uri"] = redirectUri
            }), ct);
        var tokenJson = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
            return (false, $"LinkedIn token exchange failed (HTTP {(int)response.StatusCode}): {ApiError(tokenJson)}");

        using var tokenDoc = JsonDocument.Parse(tokenJson);
        var root = tokenDoc.RootElement;
        var accessToken = GetString(root, "access_token");
        if (string.IsNullOrWhiteSpace(accessToken))
            return (false, "LinkedIn token response did not contain an access token.");

        var now = DateTimeOffset.UtcNow;
        s.LinkedInAccessToken = accessToken;
        s.LinkedInAccessTokenExpiresAt = now.AddSeconds(GetInt64(root, "expires_in", 5_184_000));
        s.LinkedInRefreshToken = GetString(root, "refresh_token");
        var refreshSeconds = GetInt64(root, "refresh_token_expires_in", 0);
        s.LinkedInRefreshTokenExpiresAt = refreshSeconds > 0 ? now.AddSeconds(refreshSeconds) : null;
        var grantedScopes = GetString(root, "scope");
        if (!string.IsNullOrWhiteSpace(grantedScopes)) s.LinkedInScopes = NormalizeScopes(grantedScopes);
        s.LinkedInOAuthState = "";
        s.LinkedInOAuthStateExpiresAt = null;

        var (profileOk, memberId, memberName, picture, profileError) =
            await LoadMemberProfileAsync(http, accessToken, ct);
        if (profileOk)
        {
            s.LinkedInMemberId = memberId;
            s.LinkedInMemberName = memberName;
            s.LinkedInMemberPictureUrl = picture;
        }
        await _db.SaveChangesAsync(ct);

        _audit.Record("LinkedIn", s.Id, "Connected",
            $"LinkedIn OAuth connected for {(memberName.Length > 0 ? memberName : "the operator")}; token expires {s.LinkedInAccessTokenExpiresAt:u}.", "operator");
        await _db.SaveChangesAsync(ct);
        return profileOk
            ? (true, $"Connected to LinkedIn as {memberName}.")
            : (false, "The token was saved, but LinkedIn did not return a usable member id: " + profileError);
    }

    public async Task DisconnectAsync(CancellationToken ct)
    {
        var s = await GetSettingsAsync(tracking: true, ct);
        s.LinkedInAccessToken = "";
        s.LinkedInAccessTokenExpiresAt = null;
        s.LinkedInRefreshToken = "";
        s.LinkedInRefreshTokenExpiresAt = null;
        s.LinkedInMemberId = "";
        s.LinkedInMemberName = "";
        s.LinkedInMemberPictureUrl = "";
        s.LinkedInOAuthState = "";
        s.LinkedInOAuthStateExpiresAt = null;
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>Publishes one approved text draft and updates its tracking identity.</summary>
    public async Task<(bool Succeeded, string Message)> PublishPostAsync(long postId, CancellationToken ct)
    {
        var post = await _db.OperatorPosts.FirstOrDefaultAsync(p => p.Id == postId, ct);
        if (post is null) return (false, "LinkedIn draft not found.");
        if (!post.Platform.Equals("linkedin", StringComparison.OrdinalIgnoreCase))
            return (false, "Only LinkedIn drafts can be published here.");
        if (post.Status != OperatorPostStatus.Draft)
            return (false, $"Post is {post.Status}, not Draft.");
        if (string.IsNullOrWhiteSpace(post.Body)) return (false, "Post body is empty.");

        var s = await GetSettingsAsync(tracking: false, ct);
        var connectionError = ConnectionError(s);
        if (connectionError is not null) return (false, connectionError);
        if (s.GlobalKillSwitch) return (false, "Global kill switch is on; outbound publishing is blocked.");

        var actor = PersonUrn(s.LinkedInMemberId);
        var payload = JsonSerializer.Serialize(new
        {
            author = actor,
            commentary = post.Body.Trim(),
            visibility = "PUBLIC",
            distribution = new
            {
                feedDistribution = "MAIN_FEED",
                targetEntities = Array.Empty<object>(),
                thirdPartyDistributionChannels = Array.Empty<object>()
            },
            lifecycleState = "PUBLISHED",
            isReshareDisabledByAuthor = false
        });

        var http = _httpFactory.CreateClient(ConnectorSupport.HttpClientName);
        using var request = ApiRequest(HttpMethod.Post, "/posts", s);
        request.Content = new StringContent(payload, Encoding.UTF8, "application/json");
        using var response = await http.SendAsync(request, ct);
        var responseText = await response.Content.ReadAsStringAsync(ct);
        if (response.StatusCode == HttpStatusCode.Unauthorized)
            return (false, "LinkedIn rejected the token. Reconnect the account and try again.");
        if (response.StatusCode != HttpStatusCode.Created && !response.IsSuccessStatusCode)
            return (false, $"LinkedIn publish failed (HTTP {(int)response.StatusCode}): {ApiError(responseText)}");

        var urn = response.Headers.TryGetValues("x-restli-id", out var ids) ? ids.FirstOrDefault() ?? "" : "";
        if (string.IsNullOrWhiteSpace(urn) && TryJson(responseText, out var responseRoot))
            urn = GetString(responseRoot, "id");
        if (string.IsNullOrWhiteSpace(urn)) urn = "linkedin-" + Guid.NewGuid().ToString("N");

        var now = DateTimeOffset.UtcNow;
        post.ExternalId = urn;
        post.Url = urn.StartsWith("urn:li:", StringComparison.OrdinalIgnoreCase)
            ? $"https://www.linkedin.com/feed/update/{urn}/" : post.Url;
        post.Status = OperatorPostStatus.Active;
        post.PostedAt = now;
        post.ScheduledAt = null;
        post.LastCheckedAt = now;
        post.UpdatedAt = now;
        await _db.SaveChangesAsync(ct);
        _audit.Record("OperatorPost", post.Id, "LinkedInPublished", $"Published LinkedIn post {urn}.", "operator");
        await _db.SaveChangesAsync(ct);
        return (true, "Published on LinkedIn.");
    }

    /// <summary>Publishes every due LinkedIn draft; one failure does not block later rows.</summary>
    public async Task<(int Published, int Failed, string Message)> PublishDueAsync(CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var dueIds = await _db.OperatorPosts.AsNoTracking()
            .Where(p => p.Platform == "linkedin" && p.Status == OperatorPostStatus.Draft &&
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
                _log.LogWarning("Scheduled LinkedIn post {PostId} was not published: {Message}", id, message);
            }
        }
        return (published, failed, $"{published} scheduled LinkedIn post(s) published; {failed} failed.");
    }

    /// <summary>
    /// Imports top-level comments for tracked posts. LinkedIn grants the required read
    /// permission only to approved Community Management apps, so a missing scope is a
    /// clear capability result rather than a sync failure disguised as success.
    /// </summary>
    public async Task<(int Imported, int CheckedPosts, string Message)> SyncEngagementAsync(CancellationToken ct)
    {
        var s = await GetSettingsAsync(tracking: false, ct);
        var connectionError = ConnectionError(s);
        if (connectionError is not null) return (0, 0, connectionError);
        if (!HasScope(s.LinkedInScopes, "r_member_social") && !HasScope(s.LinkedInScopes, "r_member_social_feed"))
            return (0, 0, "Comment monitoring requires LinkedIn's restricted r_member_social (or r_member_social_feed) permission. Posting still works without it.");

        var posts = await _db.OperatorPosts
            .Where(p => p.Platform == "linkedin" && p.Status == OperatorPostStatus.Active && p.ExternalId.StartsWith("urn:li:"))
            .OrderByDescending(p => p.PostedAt).Take(20).ToListAsync(ct);
        var http = _httpFactory.CreateClient(ConnectorSupport.HttpClientName);
        var actor = PersonUrn(s.LinkedInMemberId);
        var imported = 0;
        var checkedPosts = 0;
        foreach (var post in posts)
        {
            using var request = ApiRequest(HttpMethod.Get,
                $"/socialActions/{Uri.EscapeDataString(post.ExternalId)}/comments?count=100", s);
            using var response = await http.SendAsync(request, ct);
            var json = await response.Content.ReadAsStringAsync(ct);
            if (response.StatusCode == HttpStatusCode.Forbidden)
                return (imported, checkedPosts, "LinkedIn denied comment reads. Confirm Community Management access and the r_member_social scope, then reconnect.");
            if (!response.IsSuccessStatusCode)
            {
                _log.LogWarning("LinkedIn comments failed for {PostUrn}: HTTP {Status} {Error}",
                    post.ExternalId, (int)response.StatusCode, ApiError(json));
                continue;
            }
            if (!TryJson(json, out var root) || !root.TryGetProperty("elements", out var elements) ||
                elements.ValueKind != JsonValueKind.Array) continue;

            var observed = 0;
            foreach (var comment in elements.EnumerateArray())
            {
                observed++;
                var authorUrn = GetString(comment, "actor");
                if (authorUrn.Equals(actor, StringComparison.OrdinalIgnoreCase)) continue;
                var id = GetString(comment, "commentUrn");
                if (id.Length == 0) id = GetString(comment, "id");
                if (id.Length == 0 || await _db.EngagementDrafts.AnyAsync(d => d.Platform == "linkedin" && d.ExternalId == id, ct))
                    continue;
                var text = comment.TryGetProperty("message", out var messageEl) ? GetString(messageEl, "text") : "";
                if (text.Length == 0) continue;
                var receivedMs = comment.TryGetProperty("created", out var createdEl)
                    ? GetInt64(createdEl, "time", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())
                    : DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                var now = DateTimeOffset.UtcNow;
                _db.EngagementDrafts.Add(new EngagementDraft
                {
                    Platform = "linkedin",
                    Kind = EngagementDraftKind.CommentReply,
                    ExternalId = id,
                    OperatorPostId = post.Id,
                    ThreadUrn = post.ExternalId,
                    ParentCommentUrn = GetString(comment, "commentUrn") is { Length: > 0 } commentUrn ? commentUrn : id,
                    AuthorUrn = authorUrn,
                    AuthorName = authorUrn,
                    SourceText = text,
                    SourceUrl = post.Url,
                    ReceivedAt = DateTimeOffset.FromUnixTimeMilliseconds(receivedMs),
                    CreatedAt = now,
                    UpdatedAt = now
                });
                imported++;
            }
            post.ReplyCount = observed;
            post.LastCheckedAt = DateTimeOffset.UtcNow;
            post.UpdatedAt = DateTimeOffset.UtcNow;
            checkedPosts++;
            await _db.SaveChangesAsync(ct);
        }
        return (imported, checkedPosts, $"Checked {checkedPosts} LinkedIn post(s); imported {imported} new comment(s).");
    }

    /// <summary>Syncs what is available, then drafts all undrafted pending responses in one AI call.</summary>
    public async Task<(int Generated, string Message)> GenerateEngagementBatchAsync(
        string extraInstructions, CancellationToken ct)
    {
        var (_, _, syncMessage) = await SyncEngagementAsync(ct);
        var candidates = await _db.EngagementDrafts
            .Include(d => d.Post)
            .Where(d => d.Platform == "linkedin" && d.Status == EngagementDraftStatus.PendingReview && d.DraftText == "")
            .OrderBy(d => d.ReceivedAt).Take(20).ToListAsync(ct);
        if (candidates.Count == 0)
            return (0, "No undrafted LinkedIn engagements. " + syncMessage);

        var settings = await GetSettingsAsync(tracking: false, ct);
        var skills = await _db.Skills.AsNoTracking().Where(s => s.Enabled).ToListAsync(ct);
        var items = candidates.Select(d => new LinkedInPrompts.EngagementItem(
            d.Id, d.Kind, d.AuthorName, d.SourceText, d.Post?.Title ?? "", d.Post?.Body ?? "")).ToList();
        var prompt = LinkedInPrompts.BuildEngagementBatchPrompt(
            settings, SkillMatcher.PromptSummary(skills), items, extraInstructions);
        var timeout = TimeSpan.FromSeconds(Math.Clamp(settings.AiTimeoutSeconds * 2, 120, 600));
        var (ok, output, error, model) = await _text.GenerateTextAsync(
            AiFeature.LinkedInEngagement, prompt, settings, timeout, ct);
        if (!ok) return (0, "LinkedIn reply drafting failed: " + error);

        Dictionary<long, string> replies;
        try { replies = ParseReplies(output); }
        catch (Exception ex) { return (0, "LinkedIn reply drafting returned invalid JSON: " + ex.Message); }

        var provider = settings.AiFor(AiFeature.LinkedInEngagement).Provider;
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
        _audit.Record("EngagementDraft", 0, "LinkedInBatchDrafted",
            $"Generated {generated} LinkedIn engagement response(s) via {provider}/{model}.", "operator");
        await _db.SaveChangesAsync(ct);
        return (generated, $"Generated {generated} response draft(s). {syncMessage}");
    }

    /// <summary>
    /// One AI call reviews the operator's LinkedIn presence — the pasted whole-profile
    /// snapshot plus what the app tracks (connection state, posts, engagement, action
    /// history) — and rebuilds the pending step-by-step action plan. Every action is
    /// executed by the operator by hand; Done and Dismissed rows survive regeneration and
    /// are fed back so a new plan builds forward instead of repeating itself.
    /// </summary>
    public async Task<(int Created, string Message)> GenerateActionPlanAsync(
        string extraInstructions, CancellationToken ct)
    {
        var settings = await GetSettingsAsync(tracking: true, ct);
        var skills = await _db.Skills.AsNoTracking().Where(s => s.Enabled).ToListAsync(ct);
        var objectives = await _db.Campaigns.AsNoTracking()
            .Where(c => c.Enabled).Select(c => c.Objective).ToListAsync(ct);

        var profileText = settings.LinkedInProfileSnapshot;
        if (string.IsNullOrWhiteSpace(profileText))
        {
            // Legacy per-section rows from the retired profile studio still count as a snapshot.
            var fields = await _db.LinkedInProfileFields.AsNoTracking()
                .Where(f => f.CurrentText != "").OrderBy(f => f.SortOrder).ToListAsync(ct);
            profileText = string.Join("\n\n", fields.Select(f => f.DisplayName + ":\n" + f.CurrentText));
        }

        var activityFacts = await BuildActivityFactsAsync(settings, ct);
        var done = await _db.LinkedInActions.AsNoTracking()
            .Where(a => a.Status == LinkedInActionStatus.Done)
            .OrderByDescending(a => a.CompletedAt).Take(30).Select(a => a.Title).ToListAsync(ct);
        var dismissed = await _db.LinkedInActions.AsNoTracking()
            .Where(a => a.Status == LinkedInActionStatus.Dismissed)
            .OrderByDescending(a => a.UpdatedAt).Take(20).Select(a => a.Title).ToListAsync(ct);

        var prompt = LinkedInPrompts.BuildActionPlanPrompt(
            settings, SkillMatcher.PromptSummary(skills), objectives, profileText,
            activityFacts, done, dismissed, extraInstructions);
        var timeout = TimeSpan.FromSeconds(Math.Clamp(settings.AiTimeoutSeconds * 2, 120, 600));
        var (ok, output, error, model) = await _text.GenerateTextAsync(
            AiFeature.LinkedInProfile, prompt, settings, timeout, ct);
        if (!ok) return (0, "Action-plan review failed: " + error);

        List<(LinkedInActionCategory Category, string Title, string Why, string Steps)> actions;
        string summary;
        try { (actions, summary) = ParseActionPlan(output); }
        catch (Exception ex) { return (0, "Action-plan review returned invalid JSON: " + ex.Message); }
        if (actions.Count == 0) return (0, "The review returned no usable actions. Try again.");

        var provider = settings.AiFor(AiFeature.LinkedInProfile).Provider;
        var now = DateTimeOffset.UtcNow;
        // A new plan replaces whatever was still pending; Done/Dismissed history stays.
        var pending = await _db.LinkedInActions
            .Where(a => a.Status == LinkedInActionStatus.Pending).ToListAsync(ct);
        _db.LinkedInActions.RemoveRange(pending);
        var order = 0;
        foreach (var action in actions)
        {
            _db.LinkedInActions.Add(new LinkedInAction
            {
                Category = action.Category,
                Title = action.Title,
                Why = action.Why,
                Steps = action.Steps,
                SortOrder = order++,
                Provider = provider,
                Model = model,
                GeneratedAt = now,
                UpdatedAt = now
            });
        }
        if (!string.IsNullOrWhiteSpace(summary))
        {
            settings.LinkedInProfileReview = summary.Trim();
            settings.LinkedInProfileReviewAt = now;
        }
        await _db.SaveChangesAsync(ct);
        _audit.Record("LinkedInAction", 0, "ActionPlanGenerated",
            $"AI reviewed the LinkedIn presence via {provider}/{model}: {actions.Count} next action(s) planned.", "operator");
        await _db.SaveChangesAsync(ct);
        return (actions.Count,
            $"Plan ready: {actions.Count} next action(s). Work through them below and mark each one done.");
    }

    /// <summary>Deterministic (zero-AI-cost) presence facts the plan is grounded in.</summary>
    private async Task<List<string>> BuildActivityFactsAsync(OperatorSettings s, CancellationToken ct)
    {
        var facts = new List<string>();
        var connected = !string.IsNullOrWhiteSpace(s.LinkedInAccessToken) &&
                        !string.IsNullOrWhiteSpace(s.LinkedInMemberId);
        facts.Add(connected
            ? $"LinkedIn account is connected to the app as {s.LinkedInMemberName}; approved posts and comment replies can publish from the app."
            : "LinkedIn account is not connected to the app yet; posting happens fully by hand.");

        var posts = await _db.OperatorPosts.AsNoTracking()
            .Where(p => p.Platform == "linkedin").ToListAsync(ct);
        var published = posts.Where(p => p.Status != OperatorPostStatus.Draft).ToList();
        var scheduled = posts.Count(p => p.Status == OperatorPostStatus.Draft && p.ScheduledAt != null);
        facts.Add($"LinkedIn posts tracked: {published.Count} published, {posts.Count - published.Count} draft(s) ({scheduled} scheduled).");
        if (published.Count > 0)
            facts.Add($"Most recent published LinkedIn post was {(int)(DateTimeOffset.UtcNow - published.Max(p => p.PostedAt)).TotalDays} day(s) ago.");

        var pendingEngagements = await _db.EngagementDrafts.AsNoTracking()
            .CountAsync(d => d.Platform == "linkedin" && d.Status == EngagementDraftStatus.PendingReview, ct);
        if (pendingEngagements > 0)
            facts.Add($"{pendingEngagements} received LinkedIn comment(s)/message(s) still wait for a response in the engagement inbox.");

        var contentDrafts = await _db.ContentDrafts.AsNoTracking()
            .CountAsync(d => d.Status == ContentDraftStatus.Draft, ct);
        if (contentDrafts > 0)
            facts.Add($"{contentDrafts} unpublished content-studio draft(s) exist that could become LinkedIn material.");
        return facts;
    }

    private static (List<(LinkedInActionCategory, string, string, string)> Actions, string Summary)
        ParseActionPlan(string output)
    {
        var start = output.IndexOf('{');
        var end = output.LastIndexOf('}');
        if (start < 0 || end <= start) throw new JsonException("no JSON object found");
        using var doc = JsonDocument.Parse(output[start..(end + 1)]);
        var root = doc.RootElement;
        var actions = new List<(LinkedInActionCategory, string, string, string)>();
        if (root.TryGetProperty("actions", out var items) && items.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in items.EnumerateArray())
            {
                var title = GetString(item, "title").Trim();
                if (title.Length == 0) continue;
                var steps = "";
                if (item.TryGetProperty("steps", out var stepsEl) && stepsEl.ValueKind == JsonValueKind.Array)
                    steps = string.Join('\n', stepsEl.EnumerateArray()
                        .Where(step => step.ValueKind == JsonValueKind.String)
                        .Select(step => (step.GetString() ?? "").Trim())
                        .Where(step => step.Length > 0));
                actions.Add((ParseActionCategory(GetString(item, "category")), title,
                    GetString(item, "why").Trim(), steps));
            }
        }
        return (actions, GetString(root, "summary"));
    }

    private static LinkedInActionCategory ParseActionCategory(string value) =>
        new string(value.ToLowerInvariant().Where(char.IsLetter).ToArray()) switch
        {
            "profile" or "profileimprovement" => LinkedInActionCategory.Profile,
            "connections" or "connection" or "network" or "networking" => LinkedInActionCategory.Connections,
            "communication" or "communicate" or "messaging" or "engagement" => LinkedInActionCategory.Communication,
            "content" or "publishing" => LinkedInActionCategory.Content,
            "credibility" or "trust" or "credibilitytrust" => LinkedInActionCategory.Credibility,
            "givevalue" or "value" or "providevalue" or "giving" => LinkedInActionCategory.GiveValue,
            _ => LinkedInActionCategory.Opportunities
        };

    public async Task<EngagementDraft> CreateManualEngagementAsync(
        string author, string sourceText, EngagementDraftKind kind, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var draft = new EngagementDraft
        {
            Platform = "linkedin", Kind = kind, ExternalId = Guid.NewGuid().ToString("N"),
            AuthorName = author.Trim(), SourceText = sourceText.Trim(), ReceivedAt = now,
            CreatedAt = now, UpdatedAt = now
        };
        _db.EngagementDrafts.Add(draft);
        await _db.SaveChangesAsync(ct);
        return draft;
    }

    /// <summary>Publishes a reviewed public-comment response. Pasted private messages stay copy-only.</summary>
    public async Task<(bool Succeeded, string Message)> PublishEngagementAsync(long draftId, CancellationToken ct)
    {
        var draft = await _db.EngagementDrafts.FirstOrDefaultAsync(d => d.Id == draftId, ct);
        if (draft is null) return (false, "Engagement draft not found.");
        if (draft.Kind != EngagementDraftKind.CommentReply)
            return (false, "LinkedIn does not expose a general private inbox API. Copy this draft and send it in LinkedIn.");
        if (draft.Status == EngagementDraftStatus.Published) return (false, "This response was already published.");
        if (string.IsNullOrWhiteSpace(draft.DraftText)) return (false, "Response text is empty.");
        if (string.IsNullOrWhiteSpace(draft.ThreadUrn) || string.IsNullOrWhiteSpace(draft.ParentCommentUrn))
            return (false, "This comment is missing its LinkedIn thread identity; sync it again.");

        var s = await GetSettingsAsync(tracking: false, ct);
        var connectionError = ConnectionError(s);
        if (connectionError is not null) return (false, connectionError);
        if (s.GlobalKillSwitch) return (false, "Global kill switch is on; outbound responses are blocked.");

        var payload = JsonSerializer.Serialize(new
        {
            actor = PersonUrn(s.LinkedInMemberId),
            @object = draft.ThreadUrn,
            parentComment = draft.ParentCommentUrn,
            message = new { text = draft.DraftText.Trim() }
        });
        var http = _httpFactory.CreateClient(ConnectorSupport.HttpClientName);
        using var request = ApiRequest(HttpMethod.Post,
            $"/socialActions/{Uri.EscapeDataString(draft.ThreadUrn)}/comments", s);
        request.Content = new StringContent(payload, Encoding.UTF8, "application/json");
        using var response = await http.SendAsync(request, ct);
        var responseText = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            draft.Status = EngagementDraftStatus.Failed;
            draft.LastError = $"HTTP {(int)response.StatusCode}: {ApiError(responseText)}";
            draft.UpdatedAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync(ct);
            return (false, "LinkedIn comment publish failed: " + draft.LastError);
        }

        draft.Status = EngagementDraftStatus.Published;
        draft.PublishedAt = DateTimeOffset.UtcNow;
        draft.UpdatedAt = DateTimeOffset.UtcNow;
        draft.LastError = "";
        await _db.SaveChangesAsync(ct);
        _audit.Record("EngagementDraft", draft.Id, "LinkedInReplyPublished",
            "Published a reviewed response to a LinkedIn comment.", "operator");
        await _db.SaveChangesAsync(ct);
        return (true, "Response published as a LinkedIn comment reply.");
    }

    private HttpRequestMessage ApiRequest(HttpMethod method, string path, OperatorSettings s)
    {
        var request = new HttpRequestMessage(method, ApiRoot + path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", s.LinkedInAccessToken);
        request.Headers.TryAddWithoutValidation("X-Restli-Protocol-Version", "2.0.0");
        request.Headers.TryAddWithoutValidation("LinkedIn-Version", NormalizeApiVersion(s.LinkedInApiVersion));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return request;
    }

    private async Task<(bool Ok, string Id, string Name, string Picture, string Error)> LoadMemberProfileAsync(
        HttpClient http, string accessToken, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, UserInfoEndpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await http.SendAsync(request, ct);
        var json = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
            return (false, "", "", "", $"userinfo HTTP {(int)response.StatusCode}: {ApiError(json)}");
        if (!TryJson(json, out var root)) return (false, "", "", "", "userinfo returned invalid JSON");
        var id = GetString(root, "sub");
        var name = GetString(root, "name");
        if (name.Length == 0)
            name = string.Join(' ', new[] { GetString(root, "given_name"), GetString(root, "family_name") }
                .Where(x => x.Length > 0));
        return id.Length > 0
            ? (true, id, name.Length > 0 ? name : id, GetString(root, "picture"), "")
            : (false, "", name, GetString(root, "picture"), "userinfo had no sub/member id");
    }

    private async Task<OperatorSettings> GetSettingsAsync(bool tracking, CancellationToken ct)
    {
        var query = tracking ? _db.OperatorSettings.AsQueryable() : _db.OperatorSettings.AsNoTracking();
        return await query.FirstOrDefaultAsync(ct) ?? new OperatorSettings { Id = 1 };
    }

    private static string? ConnectionError(OperatorSettings s)
    {
        if (string.IsNullOrWhiteSpace(s.LinkedInAccessToken) || string.IsNullOrWhiteSpace(s.LinkedInMemberId))
            return "Connect the LinkedIn account first.";
        if (s.LinkedInAccessTokenExpiresAt is { } expires && expires <= DateTimeOffset.UtcNow)
            return "The LinkedIn access token expired. Reconnect the account.";
        return null;
    }

    private static string ResolveRedirectUri(OperatorSettings s, string requestCallbackUrl) =>
        string.IsNullOrWhiteSpace(s.LinkedInRedirectUri) ? requestCallbackUrl : s.LinkedInRedirectUri.Trim();

    private static string NormalizeScopes(string scopes) => string.Join(' ', scopes
        .Split((char[]?)null, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
        .Distinct(StringComparer.OrdinalIgnoreCase));

    private static bool HasScope(string scopes, string scope) => scopes
        .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
        .Contains(scope, StringComparer.OrdinalIgnoreCase);

    private static string NormalizeApiVersion(string version)
    {
        var digits = new string((version ?? "").Where(char.IsDigit).ToArray());
        return digits.Length == 6 ? digits : "202606";
    }

    private static string PersonUrn(string memberId) =>
        memberId.StartsWith("urn:li:person:", StringComparison.OrdinalIgnoreCase)
            ? memberId : "urn:li:person:" + memberId;

    private static bool FixedTimeEquals(string left, string right) =>
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(left), Encoding.UTF8.GetBytes(right));

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
            foreach (var key in new[] { "message", "error_description", "error" })
            {
                var value = GetString(root, key);
                if (value.Length > 0) return value.Length <= 500 ? value : value[..500];
            }
        }
        var compact = string.Join(' ', (body ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return compact.Length switch { 0 => "no error details", <= 500 => compact, _ => compact[..500] };
    }
}
