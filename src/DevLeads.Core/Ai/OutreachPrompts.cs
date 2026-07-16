using System.Text;
using DevLeads.Core.Entities;

namespace DevLeads.Core.Ai;

/// <summary>One queued lead inside a batched response-generation call.</summary>
public sealed class OutreachGenerationItem
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string OriginalPost { get; set; } = "";
    public string SourceKey { get; set; } = "";
    public string Url { get; set; } = "";
    public string? AuthorName { get; set; }
    public string CampaignObjective { get; set; } = "";
}

/// <summary>
/// Prompt for batched outreach-response generation: every queued lead in one model call,
/// each reply grounded strictly in that lead's original post.
/// </summary>
public static class OutreachPrompts
{
    public static string BuildBatchResponsePrompt(
        IReadOnlyList<OutreachGenerationItem> items, OperatorSettings op, string operatorSkills)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"You are writing outreach replies for {op.OperatorName} ({op.BusinessName}), a senior consultant. Expertise: {(string.IsNullOrWhiteSpace(operatorSkills) ? op.CoreSkills : operatorSkills)}.");
        sb.AppendLine("Each POST below is a real public post the consultant wants to respond to (a help request, job/contract posting, or bounty). Write one reply per post, to be sent by the consultant under their own name.");
        sb.AppendLine();
        sb.AppendLine("Non-negotiable grounding rules:");
        sb.AppendLine("- Respond ONLY to what the poster actually wrote. Reference their own specific words/details so it is obvious the post was read.");
        sb.AppendLine("- NEVER invent problems, symptoms, causes, diagnoses, prior conversations, timelines, or credentials. If the post lacks technical detail, ask one or two sharp, specific questions instead of asserting a cause.");
        sb.AppendLine("- If the post is a job/contract listing, respond to its actual stated requirements with directly relevant experience — no generic cover-letter filler.");
        sb.AppendLine("- If nothing useful can honestly be said about a post, return an empty body for that id rather than padding.");
        sb.AppendLine();
        sb.AppendLine("Style rules:");
        sb.AppendLine("- 70–150 words. Plain text only: no markdown, no emojis, no bullet lists, no subject lines, no placeholders like [name].");
        sb.AppendLine("- First person, direct, calm senior-engineer tone. No hype words, no \"I hope this finds you well\".");
        sb.AppendLine("- End with one concrete next step (a question to scope the work, or an offer to start and when).");
        sb.AppendLine("- Do not mention AI, and do not reveal how the post was found.");
        if (!string.IsNullOrWhiteSpace(op.BookingLink))
            sb.AppendLine($"- When a call is the natural next step, you may offer this scheduling link once, casually: {op.BookingLink.Trim()} — never lead with it, and skip it where a written reply is enough.");
        sb.AppendLine();
        foreach (var item in items)
        {
            sb.AppendLine($"--- POST id={item.Id} ---");
            sb.AppendLine($"Source: {item.SourceKey} ({item.Url})");
            if (!string.IsNullOrWhiteSpace(item.AuthorName)) sb.AppendLine($"Poster: {item.AuthorName}");
            if (!string.IsNullOrWhiteSpace(item.CampaignObjective))
                sb.AppendLine($"Campaign goal for this reply: {Compact(item.CampaignObjective, 220)}");
            sb.AppendLine($"Title: {item.Title}");
            sb.AppendLine("Post text:");
            sb.AppendLine(Compact(item.OriginalPost, 1600));
            sb.AppendLine();
        }
        sb.AppendLine("Respond with JSON only, no markdown fences, exactly this shape, one entry per post in the same order:");
        sb.AppendLine("{\"responses\":[{\"id\":\"r0\",\"body\":\"…\"}]}");
        return sb.ToString();
    }

    private static string Compact(string value, int max)
    {
        var compact = string.Join(' ', (value ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return compact.Length <= max ? compact : compact[..max];
    }
}
