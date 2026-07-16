using System.Text;
using DevLeads.Core.Entities;

namespace DevLeads.Core.Ai;

/// <summary>
/// Prompt for drafting one portfolio case study + the matching testimonial-request
/// message from a delivered engagement's actual record. One call produces both, grounded
/// strictly in what the work-session and engagement rows say — a case study that
/// overclaims is worse than none.
/// </summary>
public static class CaseStudyPrompts
{
    public static string BuildCaseStudyPrompt(
        string engagementTitle, string engagementDescription, string feeBand,
        string fixSummary, string clientConfirmation,
        string clientName, string clientCompany, bool anonymized,
        OperatorSettings op, string operatorSkills)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"You are writing a short portfolio case study for {op.OperatorName} ({op.BusinessName}), a senior software consultant. Expertise: {(string.IsNullOrWhiteSpace(operatorSkills) ? op.CoreSkills : operatorSkills)}.");
        sb.AppendLine("Distill the delivered engagement below into a case study a prospective client would actually read, plus a short message asking the client for a testimonial.");
        sb.AppendLine();
        sb.AppendLine("Non-negotiable grounding rules:");
        sb.AppendLine("- Use ONLY facts present in the engagement record below. Never invent metrics, timelines, revenue impact, team sizes, or quotes. If the record gives no measurable outcome, describe the outcome qualitatively (\"the site was back online\", \"the migration shipped\").");
        sb.AppendLine(anonymized
            ? "- ANONYMIZED: never name the client, their company, or anything identifying (domains, product names). Refer to them generically (\"a small e-commerce business\", \"a SaaS founder\")."
            : $"- The client may be named: {clientName}{(string.IsNullOrWhiteSpace(clientCompany) ? "" : $" ({clientCompany})")}. Use the name naturally, not in every sentence.");
        sb.AppendLine("- First person for the consultant (\"I\"). Calm, concrete senior-engineer tone; no hype, no buzzwords, no exclamation marks.");
        sb.AppendLine();
        sb.AppendLine("The testimonial request: 40–90 words, warm and low-pressure, addressed to the client, referencing the actual work, asking for two or three sentences they'd be comfortable having quoted publicly. Plain text, no placeholders.");
        sb.AppendLine();
        sb.AppendLine("--- ENGAGEMENT RECORD ---");
        sb.AppendLine($"Engagement: {engagementTitle}");
        if (!string.IsNullOrWhiteSpace(engagementDescription))
            sb.AppendLine($"Description: {Compact(engagementDescription, 800)}");
        if (!string.IsNullOrWhiteSpace(feeBand))
            sb.AppendLine($"Fee band (context only — never state fees in the case study): {feeBand}");
        if (!string.IsNullOrWhiteSpace(fixSummary))
            sb.AppendLine($"What was actually done (work-session fix summary): {Compact(fixSummary, 1200)}");
        if (!string.IsNullOrWhiteSpace(clientConfirmation))
            sb.AppendLine($"Client confirmation notes: {Compact(clientConfirmation, 400)}");
        sb.AppendLine();
        sb.AppendLine("Respond with JSON only, no markdown fences, exactly this shape:");
        sb.AppendLine("{\"title\":\"…\",\"slug\":\"kebab-case-url-slug\",\"problem\":\"2-4 sentences\",\"solution\":\"2-5 sentences\",\"outcome\":\"1-3 sentences\",\"technologies\":\"comma, separated, list\",\"testimonialRequest\":\"…\"}");
        return sb.ToString();
    }

    private static string Compact(string value, int max)
    {
        var compact = string.Join(' ', (value ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return compact.Length <= max ? compact : compact[..max];
    }
}
