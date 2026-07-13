namespace DevLeads.Core;

/// <summary>
/// Whole-word term matching for query-pack terms and pre-filter signals. Plain
/// substring Contains made short terms unusable ("rag" matched "storage", "down"
/// matched "markdown"), which forced packs into long exact phrases real posts never
/// contain. A term edge that is a letter/digit must sit on a word boundary; edges that
/// are punctuation (".net", "[hiring]") keep substring semantics so "asp.net" still
/// matches ".net". Multi-word phrases match literally.
/// </summary>
public static class TermMatch
{
    public static bool ContainsWholeTerm(string text, string term)
    {
        if (string.IsNullOrWhiteSpace(term) || term.Length > text.Length) return false;

        var requireStartBoundary = char.IsLetterOrDigit(term[0]);
        var requireEndBoundary = char.IsLetterOrDigit(term[^1]);

        var start = 0;
        while (start <= text.Length - term.Length)
        {
            var idx = text.IndexOf(term, start, StringComparison.OrdinalIgnoreCase);
            if (idx < 0) return false;

            var startOk = !requireStartBoundary || idx == 0 || !char.IsLetterOrDigit(text[idx - 1]);
            var end = idx + term.Length;
            var endOk = !requireEndBoundary || end >= text.Length || !char.IsLetterOrDigit(text[end]);
            if (startOk && endOk) return true;

            start = idx + 1;
        }
        return false;
    }
}
