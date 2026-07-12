using System.Text;
using DevLeads.Core.Entities;

namespace DevLeads.Core.Ai;

/// <summary>
/// Prompt for drafting the operator's OWN posts/ads/profiles for a specific platform
/// (reddit, craigslist, LinkedIn, Upwork, gmail outreach template) in the operator's
/// real identity and voice, informed by which past posts actually drew replies.
/// </summary>
public static class PlatformPostPrompts
{
    public static readonly string[] SupportedPlatforms = { "reddit", "craigslist", "linkedin", "upwork", "gmail" };

    public static string BuildPostPrompt(
        string platform,
        OperatorSettings op,
        string operatorSkills,
        string campaignObjective,
        IReadOnlyList<(string Title, string Body, int Replies)> referencePosts,
        string extraInstructions)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Write a {PlatformLabel(platform)} for this consultant, ready to post under their own name:");
        sb.AppendLine($"- Name: {op.OperatorName}");
        sb.AppendLine($"- Location: {op.Location}; remote availability: {op.RemoteAvailability}");
        sb.AppendLine($"- Contact email: {op.ContactEmail}");
        sb.AppendLine($"- Expertise: {(string.IsNullOrWhiteSpace(operatorSkills) ? op.CoreSkills : operatorSkills)}");
        sb.AppendLine($"- Minimum engagement: ${op.MinimumFee:0}; terms: {op.PreferredPaymentTerms}");
        if (!string.IsNullOrWhiteSpace(campaignObjective))
            sb.AppendLine($"- The post advertises this offering: {campaignObjective}");
        sb.AppendLine();
        sb.AppendLine("Platform requirements:");
        sb.AppendLine(PlatformSpec(platform));
        if (referencePosts.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Past posts for voice/content reference (reply counts show what resonated — reuse what worked, do not copy verbatim):");
            foreach (var (title, body, replies) in referencePosts.Take(2))
            {
                sb.AppendLine($"--- past post ({replies} replies): {title} ---");
                sb.AppendLine(Compact(body, 900));
            }
        }
        if (!string.IsNullOrWhiteSpace(extraInstructions))
        {
            sb.AppendLine();
            sb.AppendLine("Operator's extra instructions for this post: " + extraInstructions.Trim());
        }
        sb.AppendLine();
        sb.AppendLine("Hard rules:");
        sb.AppendLine("- Only claim experience/skills listed above. Never invent projects, employers, clients, metrics, or credentials.");
        sb.AppendLine("- NEVER describe specific past projects or \"recent work\" examples unless they appear verbatim in the reference posts above — describe capabilities, not fabricated case studies.");
        sb.AppendLine("- Direct, confident, zero hype-words; write like a 20-year engineer, not a marketer.");
        sb.AppendLine("- Do not mention AI.");
        sb.AppendLine();
        sb.AppendLine("Output: plain text only. FIRST line must be the title as \"# Title\"" +
                      (platform == "gmail" ? " (the title is the email subject line)" : "") +
                      ". No markdown fences, no commentary.");
        return sb.ToString();
    }

    /// <summary>
    /// One batched call for the post-optimization experiment: each selected post gets a
    /// rewrite with a DISTINCT named strategy, so the operator can A/B the approaches
    /// against a kept-as-is control post. Output is strict JSON.
    /// </summary>
    public static string BuildOptimizationPrompt(
        OperatorSettings op,
        string operatorSkills,
        IReadOnlyList<(long Id, string Community, string Title, string Body, double AgeDays, int Views, int Replies, int Upvotes)> posts,
        string extraInstructions)
    {
        var sb = new StringBuilder();
        sb.AppendLine("You optimize [For Hire] posts on hiring subreddits for this consultant:");
        sb.AppendLine($"- Name: {op.OperatorName}; location: {op.Location}; remote: {op.RemoteAvailability}");
        sb.AppendLine($"- Contact email: {op.ContactEmail}");
        sb.AppendLine($"- Expertise: {(string.IsNullOrWhiteSpace(operatorSkills) ? op.CoreSkills : operatorSkills)}");
        sb.AppendLine($"- Minimum engagement: ${op.MinimumFee:0}; terms: {op.PreferredPaymentTerms}");
        sb.AppendLine();
        sb.AppendLine("These near-identical posts are live and underperforming. Rewrite EACH ONE with a genuinely different strategy (different hook, positioning, and structure — e.g. outcome-led, emergency-responder, niche-specialist, proof-of-depth-led, price-anchored). Never give two posts the same strategy. The point is an A/B test against an unchanged control post, so the variants must differ enough to attribute results.");
        sb.AppendLine();
        foreach (var p in posts)
        {
            sb.AppendLine($"--- POST id={p.Id} in r/{p.Community} (live {p.AgeDays:0.#} days: {p.Views} views, {p.Replies} replies, {p.Upvotes} upvotes) ---");
            sb.AppendLine("TITLE: " + p.Title);
            sb.AppendLine("BODY: " + Compact(p.Body, 1500));
        }
        if (!string.IsNullOrWhiteSpace(extraInstructions))
        {
            sb.AppendLine();
            sb.AppendLine("Operator's extra instructions: " + extraInstructions.Trim());
        }
        sb.AppendLine();
        sb.AppendLine("Hard rules:");
        sb.AppendLine("- Titles must start with \"[For Hire]\" and fit hiring-subreddit conventions (specific, no clickbait).");
        sb.AppendLine("- Bodies 150-280 words, skimmable short paragraphs, MUST state a rate or rate range (subreddit rules require one), and end with how to contact (reddit DM or the email above).");
        sb.AppendLine("- Only claim experience/skills listed above. Never invent projects, employers, clients, metrics, or credentials.");
        sb.AppendLine("- NEVER describe specific past projects or \"recent work\" examples unless they appear verbatim in the posts above — capabilities, not fabricated case studies.");
        sb.AppendLine("- Direct, confident, zero hype-words; write like a 20-year engineer, not a marketer. Do not mention AI.");
        sb.AppendLine();
        sb.AppendLine("Output STRICT JSON only, no fences, no commentary:");
        sb.AppendLine("""{"variants":[{"postId":<id>,"approach":"<3-6 word strategy label>","title":"...","body":"...","rationale":"<1-3 sentences: what changed vs the original and why it should pull more views/replies>"}]}""");
        return sb.ToString();
    }

    private static string PlatformLabel(string platform) => platform switch
    {
        "reddit" => "reddit [For Hire] post (for r/forhire-style hiring subreddits)",
        "craigslist" => "craigslist \"computer services\" ad",
        "linkedin" => "LinkedIn post announcing consulting availability",
        "upwork" => "Upwork profile overview",
        "gmail" => "cold-outreach email template",
        _ => "post"
    };

    private static string PlatformSpec(string platform) => platform switch
    {
        "reddit" =>
            "- Title must start with \"[For Hire]\" and fit hiring-subreddit conventions (specific niche, no clickbait).\n" +
            "- 150-280 words: what they do, proof of depth (years/stack specifics), engagement types, rate framing, response-time promise, how to contact (reddit DM or email).\n" +
            "- Skimmable short paragraphs; no links other than none; no emoji.",
        "craigslist" =>
            "- Services ad for the Western Massachusetts area (Northampton/Springfield/Pioneer Valley) that also serves remote clients.\n" +
            "- Title ≤ 70 characters, concrete (what + local). Body 120-220 words, plain-spoken and trustworthy for small-business owners, no jargon walls.\n" +
            "- Include the contact email and one clear call to action. No pricing beyond an honest 'flat quotes after a short call' style line.",
        "linkedin" =>
            "- 120-220 words. Hook first line; short paragraphs; ends with a soft call to action; 3-5 relevant hashtags on the final line.\n" +
            "- Professional but human; positions the consultant's niche and who should reach out. No links in the body.",
        "upwork" =>
            "- Profile overview 150-250 words. The FIRST two sentences must sell the outcome (Upwork truncates previews).\n" +
            "- Structure: outcome promise → proof of depth (stack, years) → engagement types → why clients keep them → invitation to message.\n" +
            "- First person, no headings, no bullet spam (one short list max).",
        "gmail" =>
            "- Cold-outreach email template, 90-150 words. Greeting may use the placeholder [Name] — it is the ONLY placeholder allowed.\n" +
            "- Body: one line on why this recipient, one on what the consultant does, one credibility line, one specific ask (15-min call).\n" +
            "- End with a signature block: name, location, contact email.",
        _ => "- 150-250 words, plain text."
    };

    private static string Compact(string value, int max)
    {
        var compact = string.Join(' ', (value ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return compact.Length <= max ? compact : compact[..max];
    }
}
