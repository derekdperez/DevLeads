namespace DevLeads.Core.Templates;

public sealed record ResponseTemplate(string Key, string Name, string Channel, string Body);

/// <summary>Vetted response templates. Placeholders in [brackets] are filled per-opportunity.</summary>
public static class ResponseTemplates
{
    public const string PublicTechnicalReply = "public_technical_reply";
    public const string DirectOutreach = "direct_outreach";
    public const string CompletionSmallJob = "completion_small_job";
    public const string QuoteMessage = "quote_message";

    public static readonly IReadOnlyList<ResponseTemplate> All = new List<ResponseTemplate>
    {
        new(PublicTechnicalReply, "Public technical reply", "PublicReply",
"""
This looks like a production/runtime issue rather than a normal application bug. I would first check whether the app is failing during startup, then confirm recent config, deployment, and database connection changes.

If this is still down, I handle urgent production software and database fixes and can look at it immediately. I can usually tell quickly whether it is something that can be fixed today.
"""),

        new(DirectOutreach, "Direct outreach", "DirectMessage",
"""
I saw your post about [specific issue]. This sounds like it may be [likely cause/category].

I handle emergency software, API, database, and production fixes. I can start immediately, and for a bounded issue like this I can usually give a flat-fee quote once I see the error details and recent changes.

If you still need help, send the error message, hosting environment, and what changed most recently.
"""),

        new(CompletionSmallJob, "Completion-based small job", "DirectMessage",
"""
This looks bounded enough for a small emergency fix. I can start by checking [specific diagnostic step]. If it is the expected issue and I can resolve it quickly, payment can be due upon completion.

Before I start, I would need confirmation that you are authorized to approve the work and provide access to the affected system.
"""),

        new(QuoteMessage, "Quote message", "Email",
"""
Based on the symptoms and likely scope, I can handle this as a flat-fee emergency repair.

Scope:
- Diagnose the current production failure
- Identify the immediate cause
- Apply a fix or safe rollback if available
- Confirm the system is working again

Flat fee: $[amount]
Payment terms: due upon completion for this small bounded fix.

This does not include unrelated feature work, major rewrites, or long-term infrastructure changes unless separately agreed.
"""),
    };

    public static ResponseTemplate Get(string key) => All.First(t => t.Key == key);
}
