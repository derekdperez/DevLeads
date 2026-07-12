using System.Text;
using System.Text.Json;
using DevLeads.Core.Entities;

namespace DevLeads.Core.Ai;

/// <summary>Prompts for the content studio: topic suggestion and long-form draft generation.</summary>
public static class ContentPrompts
{
    /// <summary>
    /// Asks for publishable topics distilled from trend signals. Output is strict JSON:
    /// {"topics":[{"title","angle","rationale","interestScore","skills":[],"formats":[],"evidence":[indexes]}]}.
    /// </summary>
    public static string BuildTopicPrompt(
        IReadOnlyList<TrendSignal> signals,
        string operatorSkills,
        IReadOnlyList<string> existingTopicTitles,
        int maxTopics)
    {
        var sb = new StringBuilder();
        sb.AppendLine("You are a technical content strategist for a senior software consultant.");
        sb.AppendLine("Below are current trend signals (hot community posts, new releases, product updates) gathered in the last days, each with an index.");
        sb.AppendLine("Propose publishable content topics that (a) ride these trends while they are current, (b) showcase the consultant's expertise, and (c) a practitioner/decision-maker audience would actually read.");
        sb.AppendLine();
        sb.AppendLine("Consultant expertise (topics must credibly fit this profile):");
        sb.AppendLine(string.IsNullOrWhiteSpace(operatorSkills) ? "General senior software engineering." : operatorSkills);
        sb.AppendLine();
        sb.AppendLine("Trend signals:");
        for (var i = 0; i < signals.Count; i++)
        {
            var s = signals[i];
            sb.AppendLine($"[{i}] ({s.SourceKey}, {s.PostedAt:yyyy-MM-dd}, engagement {s.Engagement:0}) {s.Title}");
            if (!string.IsNullOrWhiteSpace(s.Snippet))
                sb.AppendLine("    " + Compact(s.Snippet, 260));
        }
        if (existingTopicTitles.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Topics already suggested earlier — do NOT repeat or trivially rephrase these:");
            foreach (var t in existingTopicTitles.Take(30)) sb.AppendLine("- " + t);
        }
        sb.AppendLine();
        sb.AppendLine($"Propose at most {maxTopics} topics. Rules:");
        sb.AppendLine("- Each topic must be grounded in at least one signal (reference signals by index in \"evidence\").");
        sb.AppendLine("- \"angle\" is the specific, opinionated take that differentiates the piece from generic coverage of the same news.");
        sb.AppendLine("- \"rationale\" says why readers care NOW (timing, pain, decision they face).");
        sb.AppendLine("- \"interestScore\" is projected audience interest 0-100; be conservative.");
        sb.AppendLine("- \"formats\" picks the best-suited from: BlogPost, Article, WhitePaper, ResearchPaper, LinkedInPost.");
        sb.AppendLine("- Prefer topics that map to real client problems (migrations, upgrades, incidents, cost, security) over pure news commentary.");
        sb.AppendLine();
        sb.AppendLine("Respond with JSON only, no markdown fences, exactly this shape:");
        sb.AppendLine("{\"topics\":[{\"title\":\"…\",\"angle\":\"…\",\"rationale\":\"…\",\"interestScore\":72,\"skills\":[\"…\"],\"formats\":[\"BlogPost\"],\"evidence\":[0,3]}]}");
        return sb.ToString();
    }

    /// <summary>
    /// Asks for a complete piece in the requested format. Output is plain markdown whose
    /// first line is "# {title}" — no JSON, so long bodies can't break on escaping.
    /// </summary>
    public static string BuildDraftPrompt(ContentTopic topic, ContentFormat format, OperatorSettings op, string operatorSkills)
    {
        var evidence = ParseEvidence(topic.EvidenceJson);
        var sb = new StringBuilder();
        sb.AppendLine($"You are ghost-writing for {op.OperatorName} of {op.BusinessName}, a senior software consultant ({(string.IsNullOrWhiteSpace(operatorSkills) ? op.CoreSkills : operatorSkills)}).");
        sb.AppendLine("Write a complete, publish-ready piece the consultant will lightly edit and post under their own name.");
        sb.AppendLine();
        sb.AppendLine("Topic: " + topic.Title);
        if (!string.IsNullOrWhiteSpace(topic.Angle)) sb.AppendLine("Angle (the piece's specific take): " + topic.Angle);
        if (!string.IsNullOrWhiteSpace(topic.Rationale)) sb.AppendLine("Why readers care now: " + topic.Rationale);
        if (evidence.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Source material (link to these where relevant; do not invent other sources):");
            foreach (var (title, url) in evidence) sb.AppendLine($"- {title} — {url}");
        }
        sb.AppendLine();
        sb.AppendLine("Format requirements:");
        sb.AppendLine(FormatSpec(format));
        sb.AppendLine();
        sb.AppendLine("Hard rules:");
        sb.AppendLine("- Never fabricate benchmarks, version numbers, quotes, statistics, or client stories. General practitioner knowledge and the source material are your only facts.");
        sb.AppendLine("- Write from real-world consulting experience: concrete failure modes, trade-offs, and decision criteria beat feature lists.");
        sb.AppendLine("- No filler phrases (\"in today's fast-paced world\", \"game-changer\", \"delve\"). No em-dash overuse.");
        sb.AppendLine("- Do not mention AI or that this was generated.");
        sb.AppendLine();
        sb.AppendLine("Output: plain markdown only. The FIRST line must be the title as \"# Title\". No preamble, no fences, no commentary after the piece.");
        return sb.ToString();
    }

    private static string FormatSpec(ContentFormat format) => format switch
    {
        ContentFormat.BlogPost =>
            "- Blog post, 800-1200 words, first-person practitioner voice.\n" +
            "- Skimmable: `##` section headings, short paragraphs, code or config snippets where they earn their place.\n" +
            "- End with a practical takeaway section (not a sales pitch).",
        ContentFormat.Article =>
            "- In-depth article, 1200-1800 words, authoritative but neutral third-person-ish voice.\n" +
            "- Structure: context → what changed → analysis of implications → guidance by reader situation.\n" +
            "- Use `##` headings; cite the source material inline as markdown links.",
        ContentFormat.WhitePaper =>
            "- White paper, 1500-2500 words, formal tone for technical decision-makers.\n" +
            "- Required sections (## headings): Executive Summary, Background, Problem Statement, Analysis, Recommendations, Conclusion.\n" +
            "- Recommendations must be actionable and sequenced (what to do first, what can wait).",
        ContentFormat.ResearchPaper =>
            "- Research-style deep dive, 1500-2500 words, academic-adjacent but readable.\n" +
            "- Required sections (## headings): Abstract, Introduction, Background & Related Work, Analysis, Discussion, Conclusion, References.\n" +
            "- References section lists the source material links; cite them in-text as [1], [2].",
        ContentFormat.LinkedInPost =>
            "- LinkedIn post, 120-250 words. NO headings, NO markdown links in the body.\n" +
            "- First line is a hook that stops the scroll; short 1-2 sentence paragraphs; one concrete insight or lesson; end with a question or light call-to-action, then 3-5 relevant hashtags on the final line.\n" +
            "- Still start the output with \"# Title\" on the first line (used internally; the post body starts after it).",
        _ => "- 800-1200 words, markdown."
    };

    public static List<(string Title, string Url)> ParseEvidence(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "[]" : json);
            return doc.RootElement.ValueKind != JsonValueKind.Array
                ? new()
                : doc.RootElement.EnumerateArray()
                    .Select(e => (
                        e.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "",
                        e.TryGetProperty("url", out var u) ? u.GetString() ?? "" : ""))
                    .Where(p => p.Item2.Length > 0)
                    .ToList();
        }
        catch { return new(); }
    }

    private static string Compact(string value, int max)
    {
        var compact = string.Join(' ', (value ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return compact.Length <= max ? compact : compact[..max];
    }
}
