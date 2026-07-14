using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using DevLeads.Core;
using DevLeads.Core.Ai;
using DevLeads.Core.Entities;
using DevLeads.Core.Skills;
using DevLeads.Infrastructure.Ai;
using DevLeads.Infrastructure.Data;

namespace DevLeads.Infrastructure.Services;

/// <summary>
/// Grows the operator's public presence: AI discovery of new platforms worth joining, and
/// the starter kit (profile bio + first post) for a platform being activated. Account
/// creation itself stays manual — signing up by hand is the honest path through every
/// platform's ToS — but everything around it (where to go, what the profile says, what
/// the first post is, tracking results) is generated and tracked here.
/// </summary>
public sealed class PlatformPresenceService
{
    private readonly DevLeadsDbContext _db;
    private readonly AiTextRouter _text;
    private readonly DiscoveryActivityTracker _activity;
    private readonly AuditService _audit;
    private readonly ILogger<PlatformPresenceService> _log;

    public PlatformPresenceService(DevLeadsDbContext db, AiTextRouter text,
        DiscoveryActivityTracker activity, AuditService audit, ILogger<PlatformPresenceService> log)
    {
        _db = db;
        _text = text;
        _activity = activity;
        _audit = audit;
        _log = log;
    }

    /// <summary>
    /// One AI call proposes new platforms (grounded in the skill profile and campaign
    /// objectives), deduplicated against everything already cataloged. New entries land
    /// as Suggested for the operator to activate or dismiss.
    /// </summary>
    public async Task<(int Created, string Message)> DiscoverPlatformsAsync(CancellationToken ct)
    {
        var settings = await _db.OperatorSettings.AsNoTracking().FirstOrDefaultAsync(ct) ?? new OperatorSettings();
        var skills = await _db.Skills.AsNoTracking().Where(s => s.Enabled).ToListAsync(ct);
        var objectives = await _db.Campaigns.AsNoTracking().Where(c => c.Enabled)
            .Select(c => c.Objective).ToListAsync(ct);
        var known = await _db.PlatformProfiles.AsNoTracking()
            .Select(p => new { p.Key, p.Name, p.Url }).ToListAsync(ct);

        var prompt = PlatformPresencePrompts.BuildDiscoveryPrompt(
            settings, SkillMatcher.PromptSummary(skills), objectives,
            known.Select(k => $"{k.Name} ({k.Url})").ToList());

        _activity.RunStarted("platform_discovery", "Searching for new platforms to post on");
        var timeout = TimeSpan.FromSeconds(Math.Clamp(settings.AiTimeoutSeconds * 2, 120, 600));
        var (ok, text, error, model) = await _text.GenerateTextAsync(AiFeature.PlatformDiscovery, prompt, settings, timeout, ct);
        if (!ok)
        {
            _activity.RunCompleted("platform_discovery", healthy: false, "Platform discovery failed: " + error);
            return (0, "Platform discovery failed: " + error);
        }

        var json = AiCliSupport.ExtractJsonObject(text);
        if (json is null)
        {
            _activity.RunCompleted("platform_discovery", healthy: false, "Platform discovery returned no JSON.");
            return (0, "The model returned no parseable JSON — try again.");
        }

        List<SuggestedPlatform> suggestions;
        try
        {
            suggestions = JsonSerializer.Deserialize<DiscoveryOutput>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })?.Platforms ?? new();
        }
        catch (JsonException ex)
        {
            _activity.RunCompleted("platform_discovery", healthy: false, "Platform discovery JSON invalid.");
            return (0, "The model returned invalid JSON: " + ex.Message);
        }

        // Dedup by key, by name, and by host — the model happily re-suggests "Toptal"
        // under a fresh slug otherwise.
        var knownKeys = known.Select(k => k.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var knownNames = known.Select(k => k.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var knownHosts = known.Select(k => LeadQualityRules.HostFromUrl(k.Url) ?? "")
            .Where(h => h.Length > 0).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var now = DateTimeOffset.UtcNow;
        var created = 0;
        foreach (var s in suggestions)
        {
            var key = Slug(s.Key ?? s.Name ?? "");
            if (key.Length == 0 || string.IsNullOrWhiteSpace(s.Name)) continue;
            var host = LeadQualityRules.HostFromUrl(s.Url ?? "") ?? "";
            if (knownKeys.Contains(key) || knownNames.Contains(s.Name!) ||
                (host.Length > 0 && knownHosts.Contains(host))) continue;

            _db.PlatformProfiles.Add(new PlatformProfile
            {
                Key = key,
                Name = s.Name!.Trim(),
                Url = s.Url ?? "",
                SignupUrl = s.SignupUrl ?? s.Url ?? "",
                Category = s.Category ?? "developer community",
                Audience = s.Audience ?? "",
                Rationale = s.Rationale ?? "",
                PostingNotes = s.PostingNotes ?? "",
                CostModel = s.CostModel ?? "free",
                RequiresResume = s.RequiresResume ?? false,
                Source = "ai",
                Status = PlatformPresenceStatus.Suggested,
                Notes = $"Suggested by {model}.",
                CreatedAt = now, UpdatedAt = now
            });
            knownKeys.Add(key);
            knownNames.Add(s.Name!);
            if (host.Length > 0) knownHosts.Add(host);
            created++;
        }
        await _db.SaveChangesAsync(ct);

        _audit.Record("PlatformProfile", 0, "PlatformDiscovery", $"{created} new platform suggestion(s) via {model}.", "system");
        await _db.SaveChangesAsync(ct);
        _activity.RunCompleted("platform_discovery", healthy: true, $"{created} new platform suggestion(s).");
        return (created, created > 0
            ? $"{created} new platform suggestion(s) added — review them below."
            : "No genuinely new platforms found this run.");
    }

    /// <summary>
    /// Writes signup packs (headline, bios, skills, rate line, first post) for the given
    /// platforms — or, when <paramref name="profileIds"/> is null, for every
    /// suggested/planned platform that doesn't have one yet. Batched
    /// <see cref="PlatformPresencePrompts.SignupPackChunkSize"/> platforms per AI call,
    /// so the whole catalog costs a handful of calls, not one per platform.
    /// </summary>
    public async Task<(int Generated, int Calls, string Message)> GenerateSignupPacksAsync(
        IReadOnlyList<long>? profileIds, long? campaignId, string extraInstructions, CancellationToken ct)
    {
        var profiles = profileIds is null
            ? await _db.PlatformProfiles
                .Where(p => (p.Status == PlatformPresenceStatus.Suggested || p.Status == PlatformPresenceStatus.Planned)
                            && p.SignupPackJson == "")
                .OrderBy(p => p.Name).ToListAsync(ct)
            : await _db.PlatformProfiles.Where(p => profileIds.Contains(p.Id)).ToListAsync(ct);
        if (profiles.Count == 0) return (0, 0, "Every platform in scope already has a signup pack.");

        var settings = await _db.OperatorSettings.AsNoTracking().FirstOrDefaultAsync(ct) ?? new OperatorSettings();
        var skills = await _db.Skills.AsNoTracking().Where(s => s.Enabled).ToListAsync(ct);
        var skillSummary = SkillMatcher.PromptSummary(skills);
        var objective = campaignId is { } cid
            ? (await _db.Campaigns.AsNoTracking().FirstOrDefaultAsync(c => c.Id == cid, ct))?.Objective ?? ""
            : "";

        var chunks = profiles.Chunk(PlatformPresencePrompts.SignupPackChunkSize).ToList();
        _activity.RunStarted("platform_signup_packs", $"Writing signup packs for {profiles.Count} platform(s) in {chunks.Count} AI call(s)");
        // Batch calls produce several long-form packs per response — give them the same
        // generous window the outreach batch gets.
        var timeout = TimeSpan.FromSeconds(Math.Clamp(settings.AiTimeoutSeconds * 4, 240, 900));

        var generated = 0;
        var calls = 0;
        var errors = new List<string>();
        foreach (var chunk in chunks)
        {
            ct.ThrowIfCancellationRequested();
            var prompt = PlatformPresencePrompts.BuildSignupPackPrompt(chunk, settings, skillSummary, objective, extraInstructions);
            calls++;
            var (ok, text, error, model) = await _text.GenerateTextAsync(AiFeature.PostDrafting, prompt, settings, timeout, ct);
            if (!ok) { errors.Add(error); continue; }

            var json = AiCliSupport.ExtractJsonObject(text);
            if (json is null) { errors.Add("no parseable JSON in the response"); continue; }
            List<PackOutput> packs;
            try
            {
                packs = JsonSerializer.Deserialize<PackListOutput>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true })?.Packs ?? new();
            }
            catch (JsonException ex) { errors.Add("invalid JSON: " + ex.Message); continue; }

            var now = DateTimeOffset.UtcNow;
            foreach (var profile in chunk)
            {
                var p = packs.FirstOrDefault(x => string.Equals(x.Key, profile.Key, StringComparison.OrdinalIgnoreCase));
                if (p is null) continue;
                var pack = new SignupPack
                {
                    Headline = p.Headline ?? "",
                    BioShort = p.BioShort ?? "",
                    BioLong = p.BioLong ?? "",
                    Skills = p.Skills ?? "",
                    RateLine = p.RateLine ?? "",
                    PostTitle = p.PostTitle ?? "",
                    PostBody = p.PostBody ?? ""
                };
                if (pack.IsEmpty) continue;
                profile.SignupPackJson = pack.ToJson();
                profile.GeneratedBio = pack.BioShort;
                // Explicitly picking a platform to pack is the "I'm joining this" signal;
                // the catalog-wide batch sweep is not.
                if (profileIds is not null && profile.Status == PlatformPresenceStatus.Suggested)
                    profile.Status = PlatformPresenceStatus.Planned;
                profile.UpdatedAt = now;
                generated++;
                _audit.Record("PlatformProfile", profile.Id, "SignupPack", $"Signup pack for {profile.Name} via {model}.", "operator");
            }
            await _db.SaveChangesAsync(ct);
        }

        var healthy = generated > 0;
        var message = generated > 0
            ? $"{generated} signup pack(s) written in {calls} AI call(s)." +
              (errors.Count > 0 ? $" {errors.Count} call(s) failed: {errors[0]}" : "")
            : "Signup pack generation failed: " + (errors.FirstOrDefault() ?? "no packs came back");
        _activity.RunCompleted("platform_signup_packs", healthy, message);
        return (generated, calls, message);
    }

    /// <summary>
    /// Marks a platform's account as created and starts tracking it. The pack's first
    /// post becomes an OperatorPost draft here (not at generation time), so batch-packing
    /// twenty suggestions doesn't flood Tracked posts with drafts for accounts that
    /// don't exist yet.
    /// </summary>
    public async Task<(bool Ok, string Message)> ActivateAsync(long profileId, CancellationToken ct)
    {
        var profile = await _db.PlatformProfiles.FirstOrDefaultAsync(p => p.Id == profileId, ct);
        if (profile is null) return (false, "Platform not found.");

        var now = DateTimeOffset.UtcNow;
        profile.Status = PlatformPresenceStatus.Active;
        profile.ActivatedAt ??= now;
        profile.UpdatedAt = now;

        var draftCreated = false;
        var pack = SignupPack.FromJson(profile.SignupPackJson);
        if (pack is not null && !string.IsNullOrWhiteSpace(pack.PostBody) &&
            !await _db.OperatorPosts.AnyAsync(p => p.Platform == profile.Key, ct))
        {
            _db.OperatorPosts.Add(new OperatorPost
            {
                Platform = profile.Key,
                ExternalId = Guid.NewGuid().ToString("N"),
                Title = string.IsNullOrWhiteSpace(pack.PostTitle) ? $"First post on {profile.Name}" : pack.PostTitle,
                Body = pack.PostBody,
                Status = OperatorPostStatus.Draft,
                Notes = $"First post from the {profile.Name} signup pack — post it there, then set the URL and mark Active.",
                PostedAt = now, CreatedAt = now, UpdatedAt = now
            });
            draftCreated = true;
        }

        _audit.Record("PlatformProfile", profile.Id, "Activated", $"{profile.Name} account created; tracking enabled.", "operator");
        await _db.SaveChangesAsync(ct);
        return (true, draftCreated
            ? $"{profile.Name} is active — its first post is waiting as a draft in Tracked posts."
            : $"{profile.Name} is active — its posts can now be tracked below.");
    }

    private sealed class PackListOutput
    {
        public List<PackOutput> Packs { get; set; } = new();
    }

    private sealed class PackOutput
    {
        public string? Key { get; set; }
        public string? Headline { get; set; }
        public string? BioShort { get; set; }
        public string? BioLong { get; set; }
        public string? Skills { get; set; }
        public string? RateLine { get; set; }
        public string? PostTitle { get; set; }
        public string? PostBody { get; set; }
    }

    private static string Slug(string value) =>
        new string(value.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());

    private sealed class DiscoveryOutput
    {
        public List<SuggestedPlatform> Platforms { get; set; } = new();
    }

    private sealed class SuggestedPlatform
    {
        public string? Key { get; set; }
        public string? Name { get; set; }
        public string? Url { get; set; }
        public string? SignupUrl { get; set; }
        public string? Category { get; set; }
        public string? Audience { get; set; }
        public string? Rationale { get; set; }
        public string? PostingNotes { get; set; }
        public string? CostModel { get; set; }
        public bool? RequiresResume { get; set; }
    }
}
