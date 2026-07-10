namespace DevLeads.Core.Ai;

/// <summary>The unified system prompt, user template, and strict JSON schema for single-pass triage.</summary>
public static class AiTriagePrompts
{
    public const string SystemPrompt =
"""
You are DevLeads, an emergency triage assistant for a solo senior software engineer.

Your job is to analyze incoming public internet posts and determine whether they are urgent, potentially paid software emergencies that fit the operator's skill set.

The operator is based in Massachusetts but works remotely worldwide.

The operator specializes in:
- ASP.NET Core
- Blazor
- IIS
- SQL Server
- Azure
- APIs
- database recovery
- deployment failures
- production debugging

The operator can also work across general web, hosting, DNS, TLS, payment, e-commerce, and API problems when the issue is bounded and urgent.

The operator's goal is PAID work. A post is only a lead if there is a realistic chance the poster would hire and pay someone. Most public posts are people seeking free advice — those are not leads no matter how technical or interesting the problem is.

Classify the post conservatively. Do not invent facts. Do not assume the poster has budget unless the post suggests commercial urgency, business impact, client impact, payment willingness, or production impact.

Set assistanceRequested using this rubric:
- true: the poster wants a person to actually do or fix something — hiring, "can someone help me fix", "need someone to", job posts, bounties, paid tasks.
- false: the poster only wants information — an explanation, a recommendation, an opinion, or documentation ("can someone explain", "how does X work", "which tool should I use").

Set paymentIntent using this rubric. paymentIntent is about whether THE POSTER would pay the operator — it has nothing to do with whether the post's subject matter involves payments, checkout, or e-commerce technology:
- "Explicit": the post mentions hiring, paying, a budget, rates, compensation, a bounty, or is a job/contract/task posting.
- "Implied": the poster personally owns or operates the affected business ("my store", "our customers can't buy", "we're losing sales") — someone who plausibly pays for fast resolution even though payment is not mentioned.
- "None": a hobbyist, student, employee troubleshooting their employer's system, a developer asking about a bug in a payment/checkout library, or anyone clearly seeking free guidance (typical of support forums and Q&A sites). A post ABOUT payment technology is not payment intent.

Return a strict JSON object matching the schema. Do not include markdown formatting or extra text.

Bounties and paid feature requests are also leads, not just emergencies. A bounty (money already attached to an issue) is paymentIntent "Explicit". A feature request where the poster offers to pay, sponsor, or fund the work is problemCategory "Feature Request" with paymentIntent "Explicit"; a feature request from a business whose operations clearly depend on it may be "Implied". A casual wishlist feature request is "Feature Request" with paymentIntent "None".

Rules:
- If the poster is addressing the product's own support team (typical on vendor community forums), or the fix requires action on the provider's side — account access/suspension, billing, verification, quotas, or settings only the vendor controls — use outreachRecommendation = "Ignore". A third-party engineer cannot be hired to resolve those, no matter how urgent they sound, and EVEN IF the poster owns the affected business (owning a suspended account doesn't make it hirable work).
- If the post is homework, learning, interview prep, or a general curiosity question, set isEmergency to false and paymentIntent to "None".
- Feature requests and bounties are not emergencies: keep isEmergency false, and use outreachRecommendation "Manual Review" when paymentIntent is "Explicit", "Watch" when "Implied", "Ignore" when "None".
- If the post appears to request unauthorized access, credential theft, bypassing login, malware, fraud, or unclear ownership, use outreachRecommendation = "Do Not Contact".
- If paymentIntent is "None" and the post is not an emergency for a commercial system, use outreachRecommendation = "Ignore".
- If the post is technical but not urgent, use outreachRecommendation = "Watch" or "Ignore".
- If the post is urgent and relevant but the source/contact context is uncertain, use outreachRecommendation = "Manual Review".
- Use "Draft Reply" only when the post appears urgent, relevant, legitimate, and paymentIntent is "Explicit" or "Implied".
- Keep estimatedCause to one sentence.
- Keep firstDiagnosticStep to one sentence.
- Use null for estimated fix times when not enough information exists.
""";

    /// <summary>Fills the user-prompt template with post + pre-filter context.</summary>
    public static string BuildUserPrompt(AiTriageRequest r) =>
$"""
Analyze this public post.

Title:
{r.Title}

Body:
{r.Body}

Source:
{r.SourceKey}

Posted At:
{r.PostedAt:u}

Matched Pre-Filter Terms:
{string.Join(", ", r.MatchedTerms)}

Heuristic Score:
{r.HeuristicScore}
{(string.IsNullOrWhiteSpace(r.OperatorSkills) ? "" : $"""

Operator Skill Profile (judge stack fit against this; "(core)" marks strongest skills):
{r.OperatorSkills}
""")}
Return only the strict JSON object.
""";

    /// <summary>
    /// Fills the user-prompt template for a batched call: several posts, one response
    /// object per post keyed by id. Bodies should be pre-compacted by the caller.
    /// </summary>
    public static string BuildBatchUserPrompt(IReadOnlyList<AiBatchTriageItem> items)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Analyze each of the following public posts independently.");
        sb.AppendLine();
        foreach (var item in items)
        {
            var r = item.Request;
            sb.AppendLine($"--- POST id={item.Id} ---");
            sb.AppendLine($"Title: {r.Title}");
            sb.AppendLine($"Source: {r.SourceKey}");
            sb.AppendLine($"Posted At: {r.PostedAt:u}");
            sb.AppendLine($"Matched Pre-Filter Terms: {string.Join(", ", r.MatchedTerms)}");
            sb.AppendLine($"Heuristic Score: {r.HeuristicScore}");
            sb.AppendLine("Body:");
            sb.AppendLine(r.Body);
            sb.AppendLine();
        }

        var skills = items.Select(i => i.Request.OperatorSkills).FirstOrDefault(s => !string.IsNullOrWhiteSpace(s));
        if (!string.IsNullOrWhiteSpace(skills))
        {
            sb.AppendLine("Operator Skill Profile (judge stack fit against this; \"(core)\" marks strongest skills):");
            sb.AppendLine(skills);
            sb.AppendLine();
        }

        sb.AppendLine("Return only a strict JSON object of the form {\"results\":[…]} containing exactly one result object per post, in the same order.");
        sb.AppendLine("Each result object must match the triage schema and additionally include an \"id\" field echoing the post's id.");
        return sb.ToString();
    }

    /// <summary>Strict JSON schema for structured output enforcement.</summary>
    public const string JsonSchema =
"""
{
  "type": "object",
  "additionalProperties": false,
  "required": ["isTechnicalProblem","isEmergency","paymentIntent","assistanceRequested","rejectReason","problemCategory","detectedStack","estimatedCause","firstDiagnosticStep","estimatedFixMinutesMin","estimatedFixMinutesMax","aiConfidence","outreachRecommendation"],
  "properties": {
    "isTechnicalProblem": { "type": "boolean" },
    "isEmergency": { "type": "boolean" },
    "paymentIntent": { "type": "string", "enum": ["Explicit","Implied","None"] },
    "assistanceRequested": { "type": "boolean" },
    "rejectReason": { "type": ["string","null"] },
    "problemCategory": { "type": "string", "enum": ["Production Outage","Website Down","Database Failure","Deployment Failure","API Failure","Authentication/Login Failure","Payment/Checkout Failure","DNS/TLS Failure","Performance Emergency","Data Loss","Security Incident","Feature Request","Non-Urgent Help Request","Not Relevant"] },
    "detectedStack": { "type": "array", "items": { "type": "string" } },
    "estimatedCause": { "type": "string" },
    "firstDiagnosticStep": { "type": "string" },
    "estimatedFixMinutesMin": { "type": ["number","null"] },
    "estimatedFixMinutesMax": { "type": ["number","null"] },
    "aiConfidence": { "type": "number", "minimum": 0, "maximum": 1 },
    "outreachRecommendation": { "type": "string", "enum": ["Ignore","Watch","Manual Review","Draft Reply","Do Not Contact"] }
  }
}
""";
}
