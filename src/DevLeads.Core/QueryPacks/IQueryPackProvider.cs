namespace DevLeads.Core.QueryPacks;

/// <summary>Supplies keyword sets (query packs) to connectors and the heuristic pre-filter.</summary>
public interface IQueryPackProvider
{
    /// <summary>Returns the terms for a named pack (empty if unknown).</summary>
    IReadOnlyList<string> GetTerms(string packName);

    /// <summary>All high-priority emergency terms across packs.</summary>
    IReadOnlyList<string> GetHighPriorityTerms();

    /// <summary>
    /// High-priority terms restricted to the named packs, so a source (and its campaign)
    /// is pre-filtered against its own signals instead of every campaign's.
    /// </summary>
    IReadOnlyList<string> GetHighPriorityTerms(IReadOnlyCollection<string> packNames);

    /// <summary>All negative / exclusion terms.</summary>
    IReadOnlyList<string> GetNegativeTerms();
}
