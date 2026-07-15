using System.Text;
using DevLeads.Core.Entities;

namespace DevLeads.Core.Ai;

/// <summary>One broken web asset inside a batched repair-offer generation call.</summary>
public sealed class WebOutreachItem
{
    public string Id { get; set; } = "";
    public string BusinessName { get; set; } = "";
    public string Host { get; set; } = "";
    public string Url { get; set; } = "";
    public string Severity { get; set; } = "";
    public string Signal { get; set; } = "";
    public string Evidence { get; set; } = "";
    public string DetectedSoftware { get; set; } = "";
}

/// <summary>
/// Prompt for batched repair-offer email generation: every selected broken-asset finding in
/// one model call, each email grounded strictly in that asset's observed, public symptom.
/// The operator sends these by hand, so the copy must be honest, specific, and low-pressure.
/// </summary>
public static class WebRescuePrompts
{
    public static string BuildOutreachBatchPrompt(
        IReadOnlyList<WebOutreachItem> items, OperatorSettings op, string operatorSkills, string extraInstructions)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"You are writing short cold repair-offer emails for {op.OperatorName} ({op.BusinessName}), a senior software consultant who fixes broken websites and web applications fast, for a flat fee. Expertise: {(string.IsNullOrWhiteSpace(operatorSkills) ? op.CoreSkills : operatorSkills)}.");
        sb.AppendLine($"Contact/reply email to give: {op.ContactEmail}. Minimum engagement fee: about ${op.MinimumFee:0}.");
        sb.AppendLine("Each ASSET below is a real, publicly-visible error or outage found on a business's own website. Write one email per asset, addressed to the site owner, offering to fix it.");
        sb.AppendLine();
        sb.AppendLine("Non-negotiable grounding rules:");
        sb.AppendLine("- Reference ONLY the specific, observed symptom given for that asset (the page and what it is showing). Never invent the cause, the business's identity, revenue, prior contact, or unrelated problems.");
        sb.AppendLine("- Be honest that you noticed the issue while their page was publicly loading; do NOT claim you were hired, referred, or scanning them intrusively. Never imply you accessed anything private or non-public.");
        sb.AppendLine("- Describe the visible impact plainly (e.g. the page is currently showing an error to visitors) and offer a fast, affordable fix. Do not overstate certainty about the root cause.");
        sb.AppendLine("- No scare tactics, no fake urgency, no security threats, no invoice/payment demands. This is a helpful, professional offer.");
        sb.AppendLine();
        sb.AppendLine("Style rules:");
        sb.AppendLine("- Subject: <=70 chars, concrete and calm (mention the site or the specific issue). Body: 60–130 words, plain text, first person.");
        sb.AppendLine("- End with one low-pressure next step (offer to take a look / a quick call) and give the reply email above. No markdown, no emojis, no placeholders like [name].");
        sb.AppendLine("- Do not mention AI or automated scanning.");
        if (!string.IsNullOrWhiteSpace(extraInstructions))
            sb.AppendLine("- Operator instructions: " + Compact(extraInstructions, 300));
        sb.AppendLine();
        foreach (var item in items)
        {
            sb.AppendLine($"--- ASSET id={item.Id} ---");
            sb.AppendLine($"Business (best guess, confirm tone accordingly): {(string.IsNullOrWhiteSpace(item.BusinessName) ? item.Host : item.BusinessName)}");
            sb.AppendLine($"Failing page: {item.Url}");
            sb.AppendLine($"Severity: {item.Severity}");
            if (!string.IsNullOrWhiteSpace(item.DetectedSoftware)) sb.AppendLine($"Detected software: {item.DetectedSoftware}");
            sb.AppendLine($"Observed symptom: {item.Signal}");
            if (!string.IsNullOrWhiteSpace(item.Evidence))
                sb.AppendLine($"Exact visible text: {Compact(item.Evidence, 400)}");
            sb.AppendLine();
        }
        sb.AppendLine("Respond with JSON only, no markdown fences, exactly this shape, one entry per asset in the same order:");
        sb.AppendLine("{\"emails\":[{\"id\":\"a0\",\"subject\":\"…\",\"body\":\"…\"}]}");
        return sb.ToString();
    }

    private static string Compact(string value, int max)
    {
        var compact = string.Join(' ', (value ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return compact.Length <= max ? compact : compact[..max];
    }
}
