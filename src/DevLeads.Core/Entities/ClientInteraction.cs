namespace DevLeads.Core.Entities;

/// <summary>
/// One logged touch with a client — a DM, email, call, or public reply, in either
/// direction. The contact history that makes follow-ups informed instead of awkward.
/// </summary>
public class ClientInteraction
{
    public long Id { get; set; }

    public long ClientId { get; set; }
    public Client? Client { get; set; }

    public DateTimeOffset OccurredAt { get; set; }

    /// <summary>"reddit dm", "email", "call", "public reply"…</summary>
    public string Channel { get; set; } = "";

    public InteractionDirection Direction { get; set; } = InteractionDirection.Outbound;

    /// <summary>What was said/decided, in one or two sentences.</summary>
    public string Summary { get; set; } = "";

    /// <summary>Link to the message/thread when one exists.</summary>
    public string Url { get; set; } = "";

    public DateTimeOffset CreatedAt { get; set; }
}
