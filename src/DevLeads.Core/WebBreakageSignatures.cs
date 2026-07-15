namespace DevLeads.Core;

/// <summary>One built-in error signature: the text to look for and how bad it is.</summary>
public readonly record struct WebBreakageSignature(string Text, WebAssetSeverity Severity, string Label);

/// <summary>
/// A curated catalog of common "this site is broken" fingerprints, so a probe works out of
/// the box before the operator adds custom error text. Matching is plain, case-insensitive
/// substring against fetched page text — these are visible public error messages, not
/// intrusive probes. Software fingerprints are kept separate so a package can be detected
/// without (yet) being a breakage on its own.
/// </summary>
public static class WebBreakageSignatures
{
    /// <summary>Visible error strings that indicate a broken or degraded public asset.</summary>
    public static readonly IReadOnlyList<WebBreakageSignature> Defaults = new[]
    {
        new WebBreakageSignature("Error establishing a database connection", WebAssetSeverity.Down, "WordPress database down"),
        new WebBreakageSignature("There has been a critical error on this website", WebAssetSeverity.Down, "WordPress fatal error"),
        new WebBreakageSignature("Fatal error:", WebAssetSeverity.Down, "PHP fatal error"),
        new WebBreakageSignature("Parse error:", WebAssetSeverity.Down, "PHP parse error"),
        new WebBreakageSignature("Uncaught Error:", WebAssetSeverity.Down, "Uncaught PHP error"),
        new WebBreakageSignature("Warning: mysqli", WebAssetSeverity.Degraded, "MySQL warning"),
        new WebBreakageSignature("Warning: require", WebAssetSeverity.Degraded, "PHP include warning"),
        new WebBreakageSignature("500 Internal Server Error", WebAssetSeverity.Down, "HTTP 500 error page"),
        new WebBreakageSignature("HTTP ERROR 500", WebAssetSeverity.Down, "HTTP 500 error page"),
        new WebBreakageSignature("502 Bad Gateway", WebAssetSeverity.Down, "502 Bad Gateway"),
        new WebBreakageSignature("503 Service Unavailable", WebAssetSeverity.Down, "503 Service Unavailable"),
        new WebBreakageSignature("504 Gateway Time-out", WebAssetSeverity.Down, "504 Gateway Timeout"),
        new WebBreakageSignature("Service Temporarily Unavailable", WebAssetSeverity.Down, "Service unavailable"),
        new WebBreakageSignature("This page isn't working", WebAssetSeverity.Down, "HTTP failure page"),
        new WebBreakageSignature("Whoops, looks like something went wrong", WebAssetSeverity.Down, "Laravel error page"),
        new WebBreakageSignature("Application error", WebAssetSeverity.Down, "Application error page"),
        new WebBreakageSignature("SQLSTATE[", WebAssetSeverity.Down, "SQL exception"),
        new WebBreakageSignature("DatabaseException", WebAssetSeverity.Down, "Database exception"),
        new WebBreakageSignature("An unhandled exception occurred", WebAssetSeverity.Down, ".NET unhandled exception"),
        new WebBreakageSignature("Server Error in '/' Application", WebAssetSeverity.Down, "ASP.NET server error"),
        new WebBreakageSignature("Runtime Error", WebAssetSeverity.Degraded, "ASP.NET runtime error"),
        new WebBreakageSignature("Traceback (most recent call last)", WebAssetSeverity.Down, "Python traceback"),
        new WebBreakageSignature("DjangoDebug", WebAssetSeverity.Down, "Django debug error"),
        new WebBreakageSignature("Cannot GET /", WebAssetSeverity.Degraded, "Node route error"),
        new WebBreakageSignature("This site can't be reached", WebAssetSeverity.Down, "Unreachable"),
        new WebBreakageSignature("There is a problem with this website's security certificate", WebAssetSeverity.Warning, "TLS certificate problem"),
        new WebBreakageSignature("Index of /", WebAssetSeverity.Warning, "Directory listing exposed"),
        new WebBreakageSignature("Under Construction", WebAssetSeverity.Warning, "Placeholder / not live"),
    };

    /// <summary>Software fingerprints: presence hints at the stack, not necessarily breakage.</summary>
    public static readonly IReadOnlyDictionary<string, string[]> SoftwareFingerprints =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["WordPress"] = new[] { "wp-content", "wp-includes", "/wp-json" },
            ["Magento"] = new[] { "Magento", "/static/version", "mage/cookies" },
            ["Shopify"] = new[] { "cdn.shopify.com", "Shopify.theme" },
            ["Drupal"] = new[] { "Drupal.settings", "/sites/default/files", "X-Generator: Drupal" },
            ["Joomla"] = new[] { "/media/jui/", "com_content", "Joomla" },
            ["Laravel"] = new[] { "laravel_session", "Whoops, looks like something went wrong" },
            ["ASP.NET"] = new[] { "__VIEWSTATE", "ASP.NET", "Server Error in" },
            ["Django"] = new[] { "csrfmiddlewaretoken", "DjangoDebug" },
        };
}
