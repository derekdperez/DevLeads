using System.Text.Json;
using DevLeads.Core.Entities;
using DevLeads.Infrastructure.Data;

namespace DevLeads.Infrastructure.Services;

/// <summary>Writes audit-trail entries for generated/sent messages and state changes.</summary>
public sealed class AuditService
{
    private readonly DevLeadsDbContext _db;
    public AuditService(DevLeadsDbContext db) => _db = db;

    public void Record(string entityType, long entityId, string eventType, string description,
        string actor = "system", object? metadata = null)
    {
        _db.AuditEvents.Add(new AuditEvent
        {
            EntityType = entityType,
            EntityId = entityId,
            EventType = eventType,
            Actor = actor,
            Description = description,
            MetadataJson = metadata is null ? "{}" : JsonSerializer.Serialize(metadata),
            CreatedAt = DateTimeOffset.UtcNow
        });
    }
}
