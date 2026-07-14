namespace DevLeads.Core.Entities;

/// <summary>
/// One editable section of the operator's LinkedIn profile (headline, about, …), kept
/// locally because LinkedIn's self-serve API cannot read profile sections. Holds the
/// operator's current text plus the latest AI-proposed rewrite; a proposal is never
/// applied automatically — the operator accepts or dismisses it per field.
/// </summary>
public class LinkedInProfileField
{
    public long Id { get; set; }

    /// <summary>Stable seed key ("headline", "about", …); rows are add-only by key.</summary>
    public string FieldKey { get; set; } = "";

    public string DisplayName { get; set; } = "";

    /// <summary>What this section is for and its practical limits; shown as a hint and given to the AI.</summary>
    public string Guidance { get; set; } = "";

    public int SortOrder { get; set; }

    /// <summary>The text as it currently reads on LinkedIn (pasted or edited by the operator).</summary>
    public string CurrentText { get; set; } = "";

    /// <summary>The latest AI rewrite awaiting operator review; empty when none is pending.</summary>
    public string SuggestedText { get; set; } = "";

    public DateTimeOffset? SuggestedAt { get; set; }
    public string Provider { get; set; } = "";
    public string Model { get; set; } = "";
    public DateTimeOffset UpdatedAt { get; set; }
}
