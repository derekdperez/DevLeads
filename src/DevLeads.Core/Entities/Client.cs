namespace DevLeads.Core.Entities;

/// <summary>
/// A real person/business the operator has a working relationship with — usually promoted
/// from a won or responding <see cref="Opportunity"/>, sometimes entered by hand. Owns the
/// engagement, interaction, and follow-up history that turns one-off leads into repeat work.
/// </summary>
public class Client
{
    public long Id { get; set; }

    /// <summary>Person's name or handle — whatever the operator actually calls them.</summary>
    public string Name { get; set; } = "";
    public string Company { get; set; } = "";

    /// <summary>Where the relationship lives: "reddit", "email", "upwork", "linkedin"…</summary>
    public string Platform { get; set; } = "";
    public string Handle { get; set; } = "";
    public string Email { get; set; } = "";
    public string ProfileUrl { get; set; } = "";

    public ClientStatus Status { get; set; } = ClientStatus.Prospect;

    /// <summary>The lead this client was promoted from (null for manual entries).</summary>
    public long? SourceOpportunityId { get; set; }

    /// <summary>Campaign that produced the relationship (null = general).</summary>
    public long? CampaignId { get; set; }

    /// <summary>Who they are, what they run, preferences, history highlights.</summary>
    public string Notes { get; set; } = "";

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public List<Engagement> Engagements { get; set; } = new();
    public List<ClientInteraction> Interactions { get; set; } = new();
    public List<FollowUp> FollowUps { get; set; } = new();
}
