namespace DevLeads.Core.Entities;

/// <summary>
/// One platform where the operator does (or could) build a public presence to attract
/// work: hiring subreddits, freelance marketplaces, dev communities, job boards, local
/// channels. Seeded from a curated catalog, extendable by AI discovery or by hand.
/// Activating a profile means "I have an account there; track my posts on it" — the
/// <see cref="Key"/> becomes a valid <see cref="OperatorPost.Platform"/> value.
/// </summary>
public class PlatformProfile
{
    public long Id { get; set; }

    /// <summary>Stable lowercase slug; matches OperatorPost.Platform once active ("reddit", "devto").</summary>
    public string Key { get; set; } = "";

    public string Name { get; set; } = "";
    public string Url { get; set; } = "";
    public string SignupUrl { get; set; } = "";

    /// <summary>"freelance marketplace", "developer community", "hiring board", "social", "local"…</summary>
    public string Category { get; set; } = "";

    /// <summary>Who is there and what they hire/read.</summary>
    public string Audience { get; set; } = "";

    /// <summary>Why this platform is worth the operator's time.</summary>
    public string Rationale { get; set; } = "";

    /// <summary>House rules and what actually works there (post shape, cadence, etiquette).</summary>
    public string PostingNotes { get; set; } = "";

    /// <summary>"free", "commission", "subscription", "vetted (free to apply)"…</summary>
    public string CostModel { get; set; } = "free";

    /// <summary>Signup/vetting asks for a resume upload — surface the stored resume in the signup pack.</summary>
    public bool RequiresResume { get; set; }

    /// <summary>"seed", "ai", or "manual" — where this entry came from.</summary>
    public string Source { get; set; } = "seed";

    public PlatformPresenceStatus Status { get; set; } = PlatformPresenceStatus.Suggested;

    /// <summary>The operator's handle/username on the platform once an account exists.</summary>
    public string Handle { get; set; } = "";

    /// <summary>The operator's own profile URL on the platform once an account exists.</summary>
    public string ProfileUrl { get; set; } = "";

    /// <summary>AI-generated profile bio for this platform (kept in sync with the pack's short bio).</summary>
    public string GeneratedBio { get; set; } = "";

    /// <summary>Serialized <see cref="SignupPack"/> — the full pre-written signup/profile copy.</summary>
    public string SignupPackJson { get; set; } = "";

    public string Notes { get; set; } = "";

    public DateTimeOffset? ActivatedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
