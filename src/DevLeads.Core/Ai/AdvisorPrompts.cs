using System.Text;
using DevLeads.Core.Entities;

namespace DevLeads.Core.Ai;

/// <summary>
/// Prompt for the daily business-advisor briefing on the Today page: one call per day
/// that turns the deterministic agenda snapshot into prioritized, opinionated guidance.
/// </summary>
public static class AdvisorPrompts
{
    public static string BuildDailyBriefingPrompt(OperatorSettings op, string agendaContext)
    {
        var sb = new StringBuilder();
        sb.AppendLine("You are the business manager for a solo software consultant. Write today's short morning briefing.");
        sb.AppendLine($"- Consultant: {op.OperatorName}, {op.Location}; remote: {op.RemoteAvailability}");
        sb.AppendLine($"- Business: finding paid software work (emergency fixes, modernization, AI/automation consulting); minimum engagement ${op.MinimumFee:0}.");
        sb.AppendLine("- Their time is split between delivering work and building a public presence that attracts work.");
        sb.AppendLine();
        sb.AppendLine("Today's live business snapshot (the ONLY facts you may use — never invent leads, clients, numbers, or events):");
        sb.AppendLine(agendaContext);
        sb.AppendLine();
        sb.AppendLine("Write the briefing as markdown with exactly these sections:");
        sb.AppendLine("## Top 3 today — the three highest-leverage actions, each one line with WHY it is first. Responding to real humans (messages, follow-ups) outranks reviewing new leads; closing money outranks everything.");
        sb.AppendLine("## Pipeline read — 2-4 sentences on what the lead/outreach numbers say and what to change.");
        sb.AppendLine("## Presence — 1-3 sentences on posts/platforms: what is working, what is stale, one concrete next move.");
        sb.AppendLine("## Watch out — at most 2 short risk reminders (overdue items, quiet clients, dead sources). Omit the section if nothing qualifies.");
        sb.AppendLine();
        sb.AppendLine("Hard rules: under 250 words total; direct and specific (name the actual lead/client/post from the snapshot); no pep talk, no generic advice, no restating the snapshot verbatim. Output plain markdown only, starting with '## Top 3 today'.");
        return sb.ToString();
    }
}
