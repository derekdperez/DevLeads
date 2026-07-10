using DevLeads.Core.Entities;

namespace DevLeads.Core.Connectors;

/// <summary>Runtime configuration passed to a connector for a single fetch.</summary>
public sealed class SourceConnectorConfig
{
    public string SourceKey { get; set; } = "";
    public int MaxItems { get; set; } = 25;
    public IReadOnlyList<string> Terms { get; set; } = Array.Empty<string>();
    public IReadOnlyDictionary<string, string> Parameters { get; set; } = new Dictionary<string, string>();
    public DateTimeOffset? Since { get; set; }

    /// <summary>Short search keywords from the operator's skill profile (strongest first).</summary>
    public IReadOnlyList<string> SkillTerms { get; set; } = Array.Empty<string>();
}

/// <summary>Reported health of a connector after a run or health check.</summary>
public sealed class ConnectorHealth
{
    public bool Healthy { get; set; } = true;
    public string Message { get; set; } = "OK";
    public int ItemCount { get; set; }
    public DateTimeOffset CheckedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// A read-only ingestion source. Fetches recent public items, respects rate limits,
/// and never sends messages. Implementations must be resilient to network failure.
/// </summary>
public interface ISourceConnector
{
    string SourceKey { get; }
    string DisplayName { get; }

    Task<IReadOnlyList<RawSourceItem>> FetchAsync(
        SourceConnectorConfig config,
        CancellationToken cancellationToken);

    Task<ConnectorHealth> CheckHealthAsync(CancellationToken cancellationToken);
}
