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
/// Turns trend signals into publishable output: AI-suggested topics, then full drafts
/// (blog posts, articles, white/research papers, LinkedIn posts) for the operator to
/// edit and publish on their own channels.
/// </summary>
public sealed class ContentStudioService
{
    private readonly DevLeadsDbContext _db;
    private readonly OpenCodeTriageProvider _openCode;
    private readonly AuditService _audit;
    private readonly DiscoveryActivityTracker _activity;
    private readonly ILogger<ContentStudioService> _log;

    public ContentStudioService(DevLeadsDbContext db, OpenCodeTriageProvider openCode,
        AuditService audit, DiscoveryActivityTracker activity, ILogger<ContentStudioService> log)
    {
        _db = db;
        _openCode = openCode;
        _audit = audit;
        _activity = activity;
        _log = log;
    }

    /// <summary>
    /// One AI call: distills the hottest recent signals into up to <paramref name="maxTopics"/>
    /// new topic suggestions. Returns (created, message).
    /// </summary>
    public async Task<(int Created, string Message)> GenerateTopicsAsync(int maxTopics, CancellationToken ct)
    {
        var settings = await GetSettingsAsync(ct);
        var cutoff = DateTimeOffset.UtcNow.AddDays(-7);
        var signals = await _db.TrendSignals.AsNoTracking()
            .Where(s => s.PostedAt >= cutoff)
            .OrderByDescending(s => s.Hotness).ThenByDescending(s => s.PostedAt)
            .Take(24)
            .ToListAsync(ct);
        if (signals.Count < 3)
            return (0, "Not enough recent trend signals yet — run a trend scan first.");

        var skills = await _db.Skills.AsNoTracking().Where(s => s.Enabled).ToListAsync(ct);
        var existingTitles = await _db.ContentTopics.AsNoTracking()
            .OrderByDescending(t => t.Id).Take(30).Select(t => t.Title).ToListAsync(ct);

        var prompt = ContentPrompts.BuildTopicPrompt(
            signals, SkillMatcher.PromptSummary(skills), existingTitles, maxTopics);

        _activity.RunStarted("content_topics", "Content studio — suggesting topics");
        var timeout = TimeSpan.FromSeconds(Math.Clamp(settings.AiTimeoutSeconds * 2, 120, 600));
        var (ok, text, error, model) = await _openCode.GenerateTextAsync(prompt, settings, timeout, ct);
        if (!ok)
        {
            _activity.RunCompleted("content_topics", healthy: false, "Topic suggestion failed: " + error);
            return (0, "Topic generation failed: " + error);
        }

        var json = OpenCodeTriageProvider.ExtractJsonObject(text);
        if (json is null)
        {
            _activity.RunCompleted("content_topics", healthy: false, "Topic suggestion returned no JSON.");
            return (0, "Topic generation returned no parsable JSON.");
        }

        List<TopicSuggestion> suggestions;
        try
        {
            var parsed = JsonSerializer.Deserialize<TopicOutput>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            suggestions = parsed?.Topics ?? new();
        }
        catch (JsonException ex)
        {
            _activity.RunCompleted("content_topics", healthy: false, "Topic JSON malformed.");
            return (0, "Topic JSON malformed: " + ex.Message);
        }

        var now = DateTimeOffset.UtcNow;
        var created = 0;
        foreach (var s in suggestions.Take(maxTopics))
        {
            if (string.IsNullOrWhiteSpace(s.Title)) continue;
            if (existingTitles.Any(t => t.Equals(s.Title, StringComparison.OrdinalIgnoreCase))) continue;

            var evidence = (s.Evidence ?? new())
                .Where(i => i >= 0 && i < signals.Count)
                .Select(i => new { title = signals[i].Title, url = signals[i].Url })
                .ToList();
            var formats = (s.Formats ?? new())
                .Where(f => Enum.TryParse<ContentFormat>(f, true, out _))
                .ToList();

            _db.ContentTopics.Add(new ContentTopic
            {
                Title = s.Title.Trim(),
                Angle = (s.Angle ?? "").Trim(),
                Rationale = (s.Rationale ?? "").Trim(),
                InterestScore = Math.Clamp(s.InterestScore, 0, 100),
                SkillsJson = JsonSerializer.Serialize(s.Skills ?? new()),
                EvidenceJson = JsonSerializer.Serialize(evidence),
                SuggestedFormatsCsv = string.Join(',', formats),
                Status = ContentTopicStatus.Suggested,
                CreatedAt = now,
                UpdatedAt = now
            });
            foreach (var i in (s.Evidence ?? new()).Where(i => i >= 0 && i < signals.Count))
            {
                var tracked = await _db.TrendSignals.FindAsync(new object[] { signals[i].Id }, ct);
                if (tracked is not null) tracked.UsedInTopic = true;
            }
            created++;
        }

        await _db.SaveChangesAsync(ct);
        _audit.Record("ContentTopic", 0, "TopicsSuggested", $"AI ({model}) suggested {created} topic(s) from {signals.Count} signals.");
        await _db.SaveChangesAsync(ct);
        _activity.RunCompleted("content_topics", healthy: true, $"Content studio: {created} new topic suggestion(s).");
        return (created, $"{created} new topic(s) suggested.");
    }

    /// <summary>One AI call: writes a full draft for a topic in the requested format.</summary>
    public async Task<(ContentDraft? Draft, string Message)> GenerateDraftAsync(
        long topicId, ContentFormat format, CancellationToken ct)
    {
        var topic = await _db.ContentTopics.FirstOrDefaultAsync(t => t.Id == topicId, ct);
        if (topic is null) return (null, "Topic not found.");

        var settings = await GetSettingsAsync(ct);
        var skills = await _db.Skills.AsNoTracking().Where(s => s.Enabled).ToListAsync(ct);
        var prompt = ContentPrompts.BuildDraftPrompt(topic, format, settings, SkillMatcher.PromptSummary(skills));

        _activity.RunStarted("content_draft", $"Content studio — writing {Spaced(format)}: {topic.Title}");
        // Long-form output takes several minutes on free CLI models; give it room.
        var timeout = TimeSpan.FromSeconds(Math.Clamp(settings.AiTimeoutSeconds * 4, 240, 900));
        var (ok, text, error, model) = await _openCode.GenerateTextAsync(prompt, settings, timeout, ct);
        if (!ok)
        {
            _activity.RunCompleted("content_draft", healthy: false, $"Draft failed: {error}");
            return (null, "Draft generation failed: " + error);
        }

        var (title, body) = SplitTitle(text, topic.Title);
        var now = DateTimeOffset.UtcNow;
        var draft = new ContentDraft
        {
            TopicId = topic.Id,
            Format = format,
            Title = title,
            BodyMarkdown = body,
            WordCount = body.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length,
            Status = ContentDraftStatus.Draft,
            Provider = "OpenCode",
            Model = model,
            CreatedAt = now,
            UpdatedAt = now
        };
        _db.ContentDrafts.Add(draft);
        topic.Status = ContentTopicStatus.Drafted;
        topic.UpdatedAt = now;
        await _db.SaveChangesAsync(ct);

        _audit.Record("ContentDraft", draft.Id, "DraftGenerated", $"{Spaced(format)} ({draft.WordCount} words) for topic '{topic.Title}' via {model}.");
        await _db.SaveChangesAsync(ct);
        _activity.RunCompleted("content_draft", healthy: true,
            $"Content studio: {Spaced(format)} drafted ({draft.WordCount} words) — {draft.Title}");
        return (draft, $"{Spaced(format)} drafted: {draft.WordCount} words.");
    }

    /// <summary>The output contract is "# Title" on line one; fall back to the topic title.</summary>
    private static (string Title, string Body) SplitTitle(string markdown, string fallbackTitle)
    {
        var lines = markdown.Replace("\r\n", "\n").Split('\n');
        var firstContent = Array.FindIndex(lines, l => !string.IsNullOrWhiteSpace(l));
        if (firstContent >= 0 && lines[firstContent].TrimStart().StartsWith('#'))
        {
            var title = lines[firstContent].TrimStart('#', ' ').Trim();
            var body = string.Join('\n', lines[(firstContent + 1)..]).Trim();
            if (title.Length > 0 && body.Length > 0) return (title, body);
        }
        return (fallbackTitle, markdown.Trim());
    }

    private static string Spaced(ContentFormat format) =>
        System.Text.RegularExpressions.Regex.Replace(format.ToString(), "(?<=[a-z])(?=[A-Z])", " ");

    private async Task<OperatorSettings> GetSettingsAsync(CancellationToken ct) =>
        await _db.OperatorSettings.AsNoTracking().FirstOrDefaultAsync(ct) ?? new OperatorSettings();

    private sealed class TopicOutput
    {
        public List<TopicSuggestion> Topics { get; set; } = new();
    }

    private sealed class TopicSuggestion
    {
        public string Title { get; set; } = "";
        public string? Angle { get; set; }
        public string? Rationale { get; set; }
        public double InterestScore { get; set; }
        public List<string>? Skills { get; set; }
        public List<string>? Formats { get; set; }
        public List<int>? Evidence { get; set; }
    }
}
