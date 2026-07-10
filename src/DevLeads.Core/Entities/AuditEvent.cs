namespace DevLeads.Core.Entities;

/// <summary>An immutable audit-trail entry for anything the system generates, sends, or changes.</summary>
public class AuditEvent
{
    public long Id { get; set; }
    public string EntityType { get; set; } = "";
    public long EntityId { get; set; }
    public string EventType { get; set; } = "";
    public string Actor { get; set; } = "system";
    public string Description { get; set; } = "";
    public string MetadataJson { get; set; } = "{}";
    public DateTimeOffset CreatedAt { get; set; }
}
