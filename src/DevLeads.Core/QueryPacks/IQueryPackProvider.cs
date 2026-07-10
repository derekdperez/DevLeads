namespace DevLeads.Core.QueryPacks;

/// <summary>Supplies keyword sets (query packs) to connectors and the heuristic pre-filter.</summary>
public interface IQueryPackProvider
{
    /// <summary>Returns the terms for a named pack (empty if unknown).</summary>
    IReadOnlyList<string> GetTerms(string packName);

    /// <summary>All high-priority emergency terms across packs.</summary>
    IReadOnlyList<string> GetHighPriorityTerms();

    /// <summary>All negative / exclusion terms.</summary>
    IReadOnlyList<string> GetNegativeTerms();
}
