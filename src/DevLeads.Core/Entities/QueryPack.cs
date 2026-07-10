namespace DevLeads.Core.Entities;

/// <summary>A named set of search/keyword terms used by connectors and the heuristic pre-filter.</summary>
public class QueryPack
{
    public long Id { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";

    /// <summary>One term per line.</summary>
    public string Terms { get; set; } = "";

    /// <summary>High-priority terms count as strong emergency signals; negatives are exclusions.</summary>
    public bool IsHighPriority { get; set; }
    public bool IsNegative { get; set; }
}
