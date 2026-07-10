namespace DevLeads.Core;

/// <summary>
/// Canonicalizes source URLs so the same post always yields the same string: drops the
/// fragment (forum reply anchors like topic#post-123 point at the same topic), tracking
/// params, and trailing slashes. Cross-source duplicate detection keys on this.
/// </summary>
public static class SourceUrlCanonicalizer
{
    /// <summary>Returns the canonical http(s) URL, or null when the input isn't one.</summary>
    public static string? Canonicalize(string? sourceUrl)
    {
        var trimmed = sourceUrl?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed)) return null;
        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri)) return null;
        if (uri.Scheme is not ("http" or "https")) return null;

        var query = uri.Query.TrimStart('?')
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Where(p => !p.StartsWith("utm_", StringComparison.OrdinalIgnoreCase) &&
                        !p.StartsWith("ref=", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var path = uri.AbsolutePath.Length > 1 ? uri.AbsolutePath.TrimEnd('/') : uri.AbsolutePath;
        return $"{uri.Scheme}://{uri.Authority}{path}{(query.Length > 0 ? "?" + string.Join('&', query) : "")}";
    }
}
