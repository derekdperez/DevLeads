using System.Text.Json;

namespace DevLeads.Core.Entities;

/// <summary>
/// Everything a platform's signup/profile form asks for, pre-written so joining takes
/// minutes of pasting instead of an hour of writing. Serialized into
/// <see cref="PlatformProfile.SignupPackJson"/>; generated in batches (several platforms
/// per AI call).
/// </summary>
public sealed class SignupPack
{
    /// <summary>Profile headline/tagline (≤ 80 chars) — the "title" field most platforms ask for.</summary>
    public string Headline { get; set; } = "";

    /// <summary>2-3 sentence first-person bio for short profile fields.</summary>
    public string BioShort { get; set; } = "";

    /// <summary>120-200 word first-person overview for long profile/about fields.</summary>
    public string BioLong { get; set; } = "";

    /// <summary>Comma-separated skill list, ordered for this platform's audience.</summary>
    public string Skills { get; set; } = "";

    /// <summary>One sentence stating rate/terms, phrased for this platform's conventions.</summary>
    public string RateLine { get; set; } = "";

    /// <summary>First post/introduction title for this platform.</summary>
    public string PostTitle { get; set; } = "";

    /// <summary>First post/introduction body, shaped by the platform's posting notes.</summary>
    public string PostBody { get; set; } = "";

    public bool IsEmpty =>
        string.IsNullOrWhiteSpace(Headline) && string.IsNullOrWhiteSpace(BioShort) &&
        string.IsNullOrWhiteSpace(BioLong) && string.IsNullOrWhiteSpace(PostBody);

    public string ToJson() => JsonSerializer.Serialize(this);

    public static SignupPack? FromJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            var pack = JsonSerializer.Deserialize<SignupPack>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return pack is { IsEmpty: false } ? pack : null;
        }
        catch (JsonException) { return null; }
    }
}
