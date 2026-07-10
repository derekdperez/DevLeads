namespace DevLeads.Core;

/// <summary>Result of the zero-cost heuristic pre-filter that gates AI analysis.</summary>
public sealed class PreFilterResult
{
    public bool ShouldAnalyzeWithAi { get; set; }
    public int KeywordHitCount { get; set; }
    public int HighPriorityHitCount { get; set; }
    public int NegativeHitCount { get; set; }
    /// <summary>Explicit willingness-to-pay signals (hire language, budgets, money amounts).</summary>
    public int PayIntentHitCount { get; set; }
    public decimal HeuristicScore { get; set; }
    public List<string> MatchedTerms { get; set; } = new();
    public string? RejectReason { get; set; }
}
