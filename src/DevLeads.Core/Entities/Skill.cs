namespace DevLeads.Core.Entities;

/// <summary>
/// One operator skill (language, framework, application, capability…). Used to score
/// how well a lead fits the operator, to filter bounty/issue searches, and to describe
/// the operator inside the AI triage prompt.
/// </summary>
public class Skill
{
    public long Id { get; set; }

    /// <summary>Display name, also the match text (case-insensitive substring), e.g. "ASP.NET Core".</summary>
    public string Name { get; set; } = "";

    /// <summary>Grouping shown on the Skills page, e.g. "Primary stack", "Databases".</summary>
    public string Category { get; set; } = "";

    /// <summary>3 = core expertise, 2 = strong, 1 = familiar.</summary>
    public int Weight { get; set; } = 2;

    /// <summary>Disabled skills stay listed but are ignored by matching and scoring.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Optional extra match terms, newline-separated (aliases, abbreviations), e.g.
    /// "efcore\nentity framework core" for "Entity Framework".
    /// </summary>
    public string Aliases { get; set; } = "";
}
