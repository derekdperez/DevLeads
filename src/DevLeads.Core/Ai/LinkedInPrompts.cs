using System.Text;
using DevLeads.Core.Entities;

namespace DevLeads.Core.Ai;

/// <summary>Grounded batched reply generation for LinkedIn comments and pasted messages.</summary>
public static class LinkedInPrompts
{
    public sealed record EngagementItem(
        long Id, EngagementDraftKind Kind, string Author, string SourceText,
        string PostTitle, string PostBody);

    public static string BuildEngagementBatchPrompt(
        OperatorSettings op, string operatorSkills, IReadOnlyList<EngagementItem> items,
        string extraInstructions)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Draft a concise LinkedIn response for EACH engagement below on behalf of this solo software consultant.");
        sb.AppendLine($"Consultant: {op.OperatorName}; expertise: {(string.IsNullOrWhiteSpace(operatorSkills) ? op.CoreSkills : operatorSkills)}");
        sb.AppendLine("The source message/comment and the operator's post are the only factual evidence. Never invent prior conversations, availability, pricing, projects, employers, or results.");
        sb.AppendLine();
        foreach (var item in items)
        {
            sb.AppendLine($"--- engagement id={item.Id}; kind={item.Kind}; from={Compact(item.Author, 100)} ---");
            if (!string.IsNullOrWhiteSpace(item.PostTitle))
                sb.AppendLine("POST TITLE: " + Compact(item.PostTitle, 240));
            if (!string.IsNullOrWhiteSpace(item.PostBody))
                sb.AppendLine("POST BODY: " + Compact(item.PostBody, 900));
            sb.AppendLine("THEIR MESSAGE: " + Compact(item.SourceText, 1200));
        }
        if (!string.IsNullOrWhiteSpace(extraInstructions))
        {
            sb.AppendLine();
            sb.AppendLine("Operator instructions: " + extraInstructions.Trim());
        }
        sb.AppendLine();
        sb.AppendLine("Rules: 35-100 words each; answer what they actually said; professional and human; no hype; no hashtags; no markdown; use the sender's name only when it is provided; ask at most one useful question. For a public comment, do not expose private contact details. For a pasted private message, a brief next-step invitation is fine.");
        sb.AppendLine("Output STRICT JSON only, no fences or commentary:");
        sb.AppendLine("{\"replies\":[{\"id\":123,\"text\":\"...\"}]}");
        return sb.ToString();
    }

    public sealed record ProfileFieldItem(string Key, string DisplayName, string Guidance, string CurrentText);

    /// <summary>
    /// One call reviews the whole locally-tracked profile: a rewrite for every section plus
    /// an overall assessment. Rewrites are proposals only — the app never applies them
    /// without the operator accepting each field.
    /// </summary>
    public static string BuildProfileRefinePrompt(
        OperatorSettings op, string operatorSkills, IReadOnlyList<string> campaignObjectives,
        IReadOnlyList<ProfileFieldItem> fields, string extraInstructions)
    {
        var sb = new StringBuilder();
        sb.AppendLine("You are a LinkedIn profile expert reviewing the profile of this solo software consultant.");
        sb.AppendLine($"Consultant: {op.OperatorName} · {op.Location} · remote availability: {op.RemoteAvailability}");
        sb.AppendLine($"Expertise: {(string.IsNullOrWhiteSpace(operatorSkills) ? op.CoreSkills : operatorSkills)}");
        foreach (var objective in campaignObjectives.Where(o => !string.IsNullOrWhiteSpace(o)))
            sb.AppendLine("Sells: " + Compact(objective, 300));
        sb.AppendLine();
        sb.AppendLine("Profile sections as they read today:");
        foreach (var field in fields)
        {
            sb.AppendLine($"--- section key={field.Key} ({field.DisplayName}; {field.Guidance}) ---");
            sb.AppendLine(string.IsNullOrWhiteSpace(field.CurrentText)
                ? "(empty — propose text from the consultant context above)"
                : Compact(field.CurrentText, 2600));
        }
        if (!string.IsNullOrWhiteSpace(extraInstructions))
        {
            sb.AppendLine();
            sb.AppendLine("Operator instructions: " + extraInstructions.Trim());
        }
        sb.AppendLine();
        sb.AppendLine("Task 1 — rewrite EVERY section above to attract paying consulting clients: specific, credible, first person, plain text (LinkedIn renders no markdown), within each section's stated limits.");
        sb.AppendLine("Ground every rewrite in the material given. Never invent employers, credentials, client names, metrics, years of experience, or results that are not stated.");
        sb.AppendLine("Task 2 — write an overall review addressed to the consultant: what the profile does well, what is weak or missing, and the highest-impact improvements in priority order (including changes outside these text sections, e.g. photo, banner, featured items, recommendations). Be direct and concrete; use short lines, not an essay.");
        sb.AppendLine("Output STRICT JSON only, no fences or commentary:");
        sb.AppendLine("{\"fields\":[{\"key\":\"headline\",\"text\":\"...\"}],\"review\":\"...\"}");
        return sb.ToString();
    }

    private static string Compact(string value, int max)
    {
        var compact = string.Join(' ', (value ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return compact.Length <= max ? compact : compact[..max];
    }
}
