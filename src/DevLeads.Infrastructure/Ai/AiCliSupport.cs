using System.Text.Json;
using System.Text.RegularExpressions;
using DevLeads.Core;
using DevLeads.Core.Ai;

namespace DevLeads.Infrastructure.Ai;

/// <summary>
/// Prompt building and output parsing shared by the CLI-backed AI providers
/// (OpenCode, Codex). Both speak the same contract — a strict-JSON triage/shortlist
/// prompt in, arbitrary agent output out — so the schema knowledge lives once here.
/// </summary>
public static class AiCliSupport
{
    public static readonly JsonSerializerOptions ParseOptions = new() { PropertyNameCaseInsensitive = true };

    // ----- prompts -----

    public static string BuildTriagePrompt(AiTriageRequest request) =>
        AiTriagePrompts.SystemPrompt +
        "\n\nThe JSON object must match this schema exactly:\n" + AiTriagePrompts.JsonSchema +
        "\n\nDo not use tools, do not read or write files — respond with the JSON object only, no markdown fences.\n\n" +
        AiTriagePrompts.BuildUserPrompt(request);

    public static string BuildBatchTriagePrompt(IReadOnlyList<AiBatchTriageItem> items) =>
        AiTriagePrompts.SystemPrompt +
        "\n\nEach result object must match this schema exactly (plus the extra \"id\" field):\n" + AiTriagePrompts.JsonSchema +
        "\n\nDo not use tools, do not read or write files — respond with the JSON object only, no markdown fences.\n\n" +
        AiTriagePrompts.BuildBatchUserPrompt(items);

    public static string BuildShortlistPrompt(IReadOnlyList<AiShortlistItem> items, int maxSelections, string campaignObjective)
    {
        var payload = items.Select(i => new
        {
            i.Id,
            i.SourceKey,
            i.Title,
            Snippet = Truncate(i.Snippet, 700),
            i.PostedAt,
            i.HeuristicScore,
            MatchedTerms = i.MatchedTerms.Take(16)
        });

        // Screen against the owning campaign's objective; the urgent-support wording is
        // only the fallback for campaign-less sources.
        var screeningGoal = string.IsNullOrWhiteSpace(campaignObjective)
            ? "You are screening public posts for profitable urgent software-support opportunities.\n"
            : "You are screening public posts for a lead-generation campaign.\n" +
              "Campaign objective: " + campaignObjective.Trim() + "\n" +
              "Pick candidates that could plausibly become the kind of paid engagement the objective describes.\n";

        return
            screeningGoal +
            "Pick only candidates worth a full expensive triage call. Favor posts where the author owns the affected business, asks for hands-on help, names a budget/pay intent, or describes customer/revenue impact.\n" +
            "Reject generic advice requests, vendor-only support/account issues, learning/homework posts, news, and low-value discussion.\n" +
            $"Return at most {maxSelections} items.\n" +
            "Respond with JSON only, exactly like: {\"selected\":[{\"id\":\"i0\",\"reason\":\"short reason\"}]}\n" +
            "Do not use tools, do not read or write files, and do not include markdown fences.\n\n" +
            "Candidates:\n" + JsonSerializer.Serialize(payload);
    }

    public sealed class ShortlistOutput
    {
        public List<ShortlistSelection> Selected { get; set; } = new();
    }

    public sealed class ShortlistSelection
    {
        public string Id { get; set; } = "";
        public string? Reason { get; set; }
    }

    // ----- output parsing -----

    private static readonly Regex AnsiPattern = new(@"\x1b\[[0-9;?]*[A-Za-z]|\x1b\][^\x07]*\x07", RegexOptions.Compiled);

    public static string StripAnsi(string text) => AnsiPattern.Replace(text, "");

    /// <summary>Extracts the first balanced JSON object from arbitrary CLI output.</summary>
    public static string? ExtractJsonObject(string text)
    {
        var start = text.IndexOf('{');
        while (start >= 0)
        {
            var depth = 0;
            var inString = false;
            for (var i = start; i < text.Length; i++)
            {
                var c = text[i];
                if (inString)
                {
                    if (c == '\\') i++;
                    else if (c == '"') inString = false;
                }
                else if (c == '"') inString = true;
                else if (c == '{') depth++;
                else if (c == '}' && --depth == 0)
                {
                    return text[start..(i + 1)];
                }
            }
            start = text.IndexOf('{', start + 1);
        }
        return null;
    }

    public static bool IsSchemaValid(AiTriageResult r) =>
        !string.IsNullOrWhiteSpace(r.ProblemCategory) &&
        !string.IsNullOrWhiteSpace(r.OutreachRecommendation);

    /// <summary>Coerces near-miss enum values back onto the strict schema instead of failing the call.</summary>
    public static void Normalize(AiTriageResult r)
    {
        if (!AiTriageResult.ProblemCategories.Contains(r.ProblemCategory, StringComparer.OrdinalIgnoreCase))
            r.ProblemCategory = r.IsEmergency ? "Production Outage" : "Non-Urgent Help Request";
        else
            r.ProblemCategory = AiTriageResult.ProblemCategories.First(c =>
                c.Equals(r.ProblemCategory, StringComparison.OrdinalIgnoreCase));

        if (!AiTriageResult.OutreachRecommendations.Contains(r.OutreachRecommendation, StringComparer.OrdinalIgnoreCase))
            r.OutreachRecommendation = "Manual Review";
        else
            r.OutreachRecommendation = AiTriageResult.OutreachRecommendations.First(c =>
                c.Equals(r.OutreachRecommendation, StringComparison.OrdinalIgnoreCase));

        // Unknown/missing payment intent stays "" (neutral) — only a deliberate value
        // should influence scoring.
        r.PaymentIntent = AiTriageResult.PaymentIntents.FirstOrDefault(v =>
            v.Equals(r.PaymentIntent, StringComparison.OrdinalIgnoreCase)) ?? "";

        r.AiConfidence = Math.Clamp(r.AiConfidence, 0m, 1m);
    }

    public static string Truncate(string s, int max) => s.Length <= max ? s : s[..max];
}
