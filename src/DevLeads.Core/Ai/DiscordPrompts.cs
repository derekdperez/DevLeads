using System.Text;
using DevLeads.Core.Entities;

namespace DevLeads.Core.Ai;

/// <summary>
/// Grounded batched reply generation for Discord: replies to the operator's tracked
/// posts, mentions in monitored channels, and pasted DMs. Discord is a community first —
/// the replies must build the operator's reputation (helpful senior engineer), never
/// hard-sell, so a good answer today becomes a client tomorrow.
/// </summary>
public static class DiscordPrompts
{
    public sealed record EngagementItem(
        long Id, EngagementDraftKind Kind, string Author, string SourceText,
        string PostTitle, string PostBody, string Community);

    public static string BuildEngagementBatchPrompt(
        OperatorSettings op, string operatorSkills, IReadOnlyList<EngagementItem> items,
        string extraInstructions)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Draft a Discord reply for EACH message below on behalf of this solo software consultant.");
        sb.AppendLine($"Consultant: {op.OperatorName}; expertise: {(string.IsNullOrWhiteSpace(operatorSkills) ? op.CoreSkills : operatorSkills)}");
        sb.AppendLine("Their message and the operator's own post are the only factual evidence. Never invent prior conversations, availability, pricing, projects, employers, or results.");
        sb.AppendLine();
        foreach (var item in items)
        {
            sb.AppendLine($"--- engagement id={item.Id}; kind={item.Kind}; from={Compact(item.Author, 100)}{(item.Community.Length > 0 ? "; in " + Compact(item.Community, 120) : "")} ---");
            if (!string.IsNullOrWhiteSpace(item.PostTitle))
                sb.AppendLine("OPERATOR'S POST (label): " + Compact(item.PostTitle, 240));
            if (!string.IsNullOrWhiteSpace(item.PostBody))
                sb.AppendLine("OPERATOR'S POST (text): " + Compact(item.PostBody, 900));
            sb.AppendLine("THEIR MESSAGE: " + Compact(item.SourceText, 1200));
        }
        if (!string.IsNullOrWhiteSpace(extraInstructions))
        {
            sb.AppendLine();
            sb.AppendLine("Operator instructions: " + extraInstructions.Trim());
        }
        sb.AppendLine();
        sb.AppendLine("Rules: chat-native Discord tone — short lines, plain words, warm but professional; 15-80 words each; answer what they actually said; if they ask something technical, give a genuinely useful answer first and let competence do the selling; no hype, no hashtags, no emoji spam (one is fine), no @everyone/@here; light Discord markdown only (**bold**, `code`); ask at most one useful question; for a public reply never post private contact details — invite a DM instead; for a DM a concrete next step (rate, booking a call) is fine. Never argue or get defensive: a gracious reply in public is marketing.");
        sb.AppendLine("Output STRICT JSON only, no fences or commentary:");
        sb.AppendLine("{\"replies\":[{\"id\":123,\"text\":\"...\"}]}");
        return sb.ToString();
    }

    private static string Compact(string value, int max)
    {
        var compact = string.Join(' ', (value ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return compact.Length <= max ? compact : compact[..max];
    }
}
