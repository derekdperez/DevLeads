namespace DevLeads.Core.Entities;

/// <summary>
/// A reusable definition of what "broken" looks like for the Site rescue scanner: a
/// software package to fingerprint, the error text/behaviour that signals a failure, the
/// extra public paths worth checking, and the search queries used to discover unknown
/// broken sites. One probe is run against a batch of targets to produce
/// <see cref="WebAssetFinding"/> rows.
/// </summary>
public class WebScanProbe
{
    public long Id { get; set; }

    /// <summary>Short operator-facing name ("WordPress DB/PHP errors", "Generic 5xx outage").</summary>
    public string Name { get; set; } = "";

    /// <summary>What this probe is for and who it targets.</summary>
    public string Description { get; set; } = "";

    /// <summary>
    /// Software package/fingerprint to look for (e.g. "WordPress", "Magento", "Laravel").
    /// Optional; when set, a match plus an error signal ranks a finding higher.
    /// </summary>
    public string SoftwarePackage { get; set; } = "";

    /// <summary>Newline-separated error strings that, when present in a response, flag breakage.</summary>
    public string ErrorSignatures { get; set; } = "";

    /// <summary>
    /// Newline-separated extra public paths to GET beyond the homepage (light probing only:
    /// public status/health/error pages, never admin or auth endpoints).
    /// </summary>
    public string PathsToCheck { get; set; } = "";

    /// <summary>Newline-separated search-engine queries used for discovery mode.</summary>
    public string DiscoveryQueries { get; set; } = "";

    /// <summary>Also treat any HTTP 5xx / connection failure as breakage, even with no signature match.</summary>
    public bool FlagServerErrors { get; set; } = true;

    public bool Enabled { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>Last scan run using this probe, and what it produced.</summary>
    public DateTimeOffset? LastRunAt { get; set; }
    public int LastRunChecked { get; set; }
    public int LastRunFound { get; set; }
}
