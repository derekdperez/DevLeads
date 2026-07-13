namespace DevLeads.Core;

/// <summary>
/// Lightweight fallback language detection used before scoring. Structured AI triage is
/// authoritative when available; these rules ensure outages cannot bypass the language
/// penalty. The detector intentionally stays conservative for code-heavy Latin text.
/// </summary>
public static class LanguageDetection
{
    public static string Detect(string title, string body)
    {
        var text = $"{title}\n{body}".ToLowerInvariant();
        if (text.Any(c => c is >= '\u3040' and <= '\u30ff')) return "ja";
        if (text.Any(c => c is >= '\uac00' and <= '\ud7af')) return "ko";
        if (text.Any(c => c is >= '\u4e00' and <= '\u9fff')) return "zh";
        if (text.Any(c => c is >= '\u0400' and <= '\u04ff')) return "ru";
        if (text.Any(c => c is >= '\u0600' and <= '\u06ff')) return "ar";

        var padded = " " + text + " ";
        static int Hits(string value, string[] words) =>
            words.Count(word => value.Contains(word, StringComparison.Ordinal));
        var candidates = new[]
        {
            (Code: "es", Hits: Hits(padded, new[] { " el ", " la ", " los ", " para ", " necesito ", " necesita ", " ayuda ", " con ", " una " })),
            (Code: "pt", Hits: Hits(padded, new[] { " para ", " preciso ", " precisa ", " ajuda ", " não ", " uma ", " com " })),
            (Code: "fr", Hits: Hits(padded, new[] { " le ", " la ", " les ", " pour ", " besoin ", " avec ", " une " })),
            (Code: "de", Hits: Hits(padded, new[] { " der ", " die ", " das ", " für ", " brauche ", " mit ", " eine " })),
            (Code: "it", Hits: Hits(padded, new[] { " il ", " la ", " per ", " bisogno ", " aiuto ", " con ", " una " }))
        };
        var best = candidates.OrderByDescending(candidate => candidate.Hits).First();
        return best.Hits >= 3 ? best.Code : "en";
    }
}
