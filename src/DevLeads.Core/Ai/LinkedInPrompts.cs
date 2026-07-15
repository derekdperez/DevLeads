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

    /// <summary>
    /// One call reviews everything the app knows about the operator's LinkedIn presence
    /// (the pasted whole-profile snapshot, tracked activity, completed/dismissed actions)
    /// and plans the concrete next actions across every presence-building category. The
    /// operator executes each step by hand — the self-serve API cannot automate any of it.
    /// </summary>
    public static string BuildActionPlanPrompt(
        OperatorSettings op, string operatorSkills, IReadOnlyList<string> campaignObjectives,
        string profileText, IReadOnlyList<string> activityFacts,
        IReadOnlyList<string> doneActions, IReadOnlyList<string> dismissedActions,
        string extraInstructions)
    {
        var sb = new StringBuilder();
        sb.AppendLine("You are a LinkedIn growth coach for this solo software consultant. Review their profile and activity, then plan the exact next actions they should take by hand on LinkedIn.");
        sb.AppendLine($"Consultant: {op.OperatorName} · {op.Location} · remote availability: {op.RemoteAvailability}");
        sb.AppendLine($"Expertise: {(string.IsNullOrWhiteSpace(operatorSkills) ? op.CoreSkills : operatorSkills)}");
        foreach (var objective in campaignObjectives.Where(o => !string.IsNullOrWhiteSpace(o)))
            sb.AppendLine("Sells: " + Compact(objective, 300));
        sb.AppendLine();
        sb.AppendLine("THEIR PROFILE, as pasted by the consultant (anything missing here really is missing from the profile):");
        sb.AppendLine(string.IsNullOrWhiteSpace(profileText)
            ? "(no snapshot pasted yet — assume a bare default profile and make pasting/completing the profile part of the plan)"
            : Compact(profileText, 7000));
        sb.AppendLine();
        sb.AppendLine("CURRENT ACTIVITY tracked by their lead-gen app:");
        foreach (var fact in activityFacts) sb.AppendLine("- " + Compact(fact, 300));
        if (doneActions.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("ALREADY COMPLETED (build on these; do not repeat them):");
            foreach (var title in doneActions) sb.AppendLine("- " + Compact(title, 200));
        }
        if (dismissedActions.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("DECLINED BEFORE (not interested; do not propose again):");
            foreach (var title in dismissedActions) sb.AppendLine("- " + Compact(title, 200));
        }
        if (!string.IsNullOrWhiteSpace(extraInstructions))
        {
            sb.AppendLine();
            sb.AppendLine("Operator instructions: " + extraInstructions.Trim());
        }
        sb.AppendLine();
        sb.AppendLine("Plan 2-3 actions for EACH of these categories, using these exact category keys:");
        sb.AppendLine("- profile: improve the profile itself (headline, about, featured, photo, banner, skills, services)");
        sb.AppendLine("- connections: find and invite relevant new connections");
        sb.AppendLine("- communication: communicate with connections — messages, comment replies, follow-ups");
        sb.AppendLine("- opportunities: find and pursue paid consulting opportunities");
        sb.AppendLine("- content: produce professional, genuinely valuable content");
        sb.AppendLine("- credibility: earn credibility and trust (recommendations, proof of work, consistency)");
        sb.AppendLine("- give_value: provide value to others with no immediate ask");
        sb.AppendLine();
        sb.AppendLine("Each action needs: a short imperative title; why — one sentence tied to THIS consultant's profile or activity; steps — 3 to 6 numbered-free step strings, each one concrete instruction executable this week (what to open, whom to search for, what to write about). Order actions within a category by impact.");
        sb.AppendLine("Ground everything in the material given. Never invent employers, credentials, client names, metrics, or results. Suggest only ethical, ToS-compliant actions — no automation of LinkedIn itself, no connection spam, no engagement pods.");
        sb.AppendLine("Also write a summary addressed to the consultant: what the profile and activity do well, what is weak or missing, and the single highest-impact focus right now. Short lines, direct and concrete.");
        sb.AppendLine("Output STRICT JSON only, no fences or commentary:");
        sb.AppendLine("{\"summary\":\"...\",\"actions\":[{\"category\":\"profile\",\"title\":\"...\",\"why\":\"...\",\"steps\":[\"...\"]}]}");
        return sb.ToString();
    }

    private static string Compact(string value, int max)
    {
        var compact = string.Join(' ', (value ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return compact.Length <= max ? compact : compact[..max];
    }
}
