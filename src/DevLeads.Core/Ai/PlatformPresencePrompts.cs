using System.Text;
using DevLeads.Core.Entities;

namespace DevLeads.Core.Ai;

/// <summary>
/// Prompts for the platform-presence feature on My posts: discovering new platforms worth
/// posting on, and generating the starter kit (profile bio + first post) for one of them.
/// </summary>
public static class PlatformPresencePrompts
{
    /// <summary>Platforms per batched signup-pack call — each pack is long-form output, so
    /// small chunks keep free-tier models from truncating the JSON.</summary>
    public const int SignupPackChunkSize = 5;

    public static string BuildDiscoveryPrompt(
        OperatorSettings op,
        string operatorSkills,
        IReadOnlyList<string> campaignObjectives,
        IReadOnlyList<string> knownPlatforms)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Suggest NEW online platforms where this solo software consultant should build a public presence to win paid work and professional contacts:");
        sb.AppendLine($"- Name: {op.OperatorName}; location: {op.Location}; remote: {op.RemoteAvailability}");
        sb.AppendLine($"- Expertise: {(string.IsNullOrWhiteSpace(operatorSkills) ? op.CoreSkills : operatorSkills)}");
        sb.AppendLine($"- Minimum engagement: ${op.MinimumFee:0}");
        foreach (var objective in campaignObjectives.Where(o => !string.IsNullOrWhiteSpace(o)))
            sb.AppendLine($"- Offering: {objective}");
        sb.AppendLine();
        sb.AppendLine("Platforms already known (NEVER suggest these or near-duplicates of them):");
        sb.AppendLine(string.Join(", ", knownPlatforms));
        sb.AppendLine();
        sb.AppendLine("Rules:");
        sb.AppendLine("- 5 to 10 suggestions, each a real, currently operating platform/community/board a solo consultant can join today.");
        sb.AppendLine("- Prefer free or commission-based over paid; prefer places where individual reputation compounds (communities, Q&A, niche boards) alongside 1-2 marketplaces at most.");
        sb.AppendLine("- Include specific communities where relevant (a particular subreddit, Discord server, or forum), not just the parent site.");
        sb.AppendLine("- audience = who is there and what they hire/read; rationale = why THIS consultant wins there; postingNotes = the local etiquette and the post shape that works.");
        sb.AppendLine("- category must be one of: \"freelance marketplace\", \"developer community\", \"hiring board\", \"social\", \"local\", \"content\".");
        sb.AppendLine("- costModel must be one of: \"free\", \"commission\", \"subscription\", \"vetted (free to apply)\".");
        sb.AppendLine("- key = short lowercase slug, letters/digits only.");
        sb.AppendLine("- requiresResume = true only when signup or vetting asks for a resume/CV upload.");
        sb.AppendLine();
        sb.AppendLine("Output STRICT JSON only, no fences, no commentary:");
        sb.AppendLine("""{"platforms":[{"key":"...","name":"...","url":"https://...","signupUrl":"https://...","category":"...","audience":"...","rationale":"...","postingNotes":"...","costModel":"...","requiresResume":false}]}""");
        return sb.ToString();
    }

    /// <summary>
    /// One batched call writes the complete signup pack for several platforms at once —
    /// every field a signup/profile form asks for plus the first post, each shaped by
    /// that platform's audience and posting notes. This is the AI-cost lever: N platforms
    /// become ceil(N/<see cref="SignupPackChunkSize"/>) calls.
    /// </summary>
    public static string BuildSignupPackPrompt(
        IReadOnlyList<PlatformProfile> platforms,
        OperatorSettings op,
        string operatorSkills,
        string campaignObjective,
        string extraInstructions)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Write a complete signup pack for EACH platform below, for this consultant creating accounts there:");
        sb.AppendLine($"- Name: {op.OperatorName}; location: {op.Location}; remote: {op.RemoteAvailability}");
        sb.AppendLine($"- Contact email: {op.ContactEmail}");
        sb.AppendLine($"- Expertise: {(string.IsNullOrWhiteSpace(operatorSkills) ? op.CoreSkills : operatorSkills)}");
        sb.AppendLine($"- Minimum engagement: ${op.MinimumFee:0}; terms: {op.PreferredPaymentTerms}");
        if (!string.IsNullOrWhiteSpace(campaignObjective))
            sb.AppendLine($"- Current offering focus: {campaignObjective}");
        if (!string.IsNullOrWhiteSpace(extraInstructions))
            sb.AppendLine("Operator's extra instructions: " + extraInstructions.Trim());
        sb.AppendLine();
        foreach (var p in platforms)
        {
            sb.AppendLine($"--- PLATFORM key={p.Key} · {p.Name} ({p.Category}) ---");
            if (!string.IsNullOrWhiteSpace(p.Audience)) sb.AppendLine("Audience: " + p.Audience);
            if (!string.IsNullOrWhiteSpace(p.PostingNotes)) sb.AppendLine("What works there: " + p.PostingNotes);
            if (p.RequiresResume) sb.AppendLine("Signup asks for a resume upload — profile copy should complement a resume, not restate one.");
        }
        sb.AppendLine();
        sb.AppendLine("For EACH platform produce, tailored to ITS audience and conventions (never reuse the same text across platforms):");
        sb.AppendLine("- headline: profile headline/tagline, max 80 characters, concrete niche over generic seniority.");
        sb.AppendLine("- bioShort: 2-3 sentence first-person bio for short profile fields.");
        sb.AppendLine("- bioLong: 120-200 word first-person overview: outcome promise → proof of depth (stack, years) → engagement types → who should reach out.");
        sb.AppendLine("- skills: comma-separated skill list ordered by what THAT platform's audience hires for.");
        sb.AppendLine("- rateLine: one sentence stating rate/terms the way that platform expects (omit numbers where rates are gauche, state them where required).");
        sb.AppendLine("- postTitle + postBody: the consultant's FIRST post/introduction there, following the platform's conventions — an introduction where self-promotion is unwelcome, an availability post where it is expected.");
        sb.AppendLine();
        sb.AppendLine("Hard rules:");
        sb.AppendLine("- Only claim experience/skills listed above. Never invent projects, employers, clients, metrics, or credentials.");
        sb.AppendLine("- Direct, confident, zero hype-words; write like a 20-year engineer, not a marketer. Do not mention AI.");
        sb.AppendLine();
        sb.AppendLine("Output STRICT JSON only, no fences, no commentary — one object per platform, keyed by its key:");
        sb.AppendLine("""{"packs":[{"key":"<platform key>","headline":"...","bioShort":"...","bioLong":"...","skills":"...","rateLine":"...","postTitle":"...","postBody":"..."}]}""");
        return sb.ToString();
    }
}
