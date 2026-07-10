namespace DevLeads.Core.Templates;

public sealed record EmergencyChecklist(string Key, string Name, string[] Items);

/// <summary>Diagnostic checklists surfaced when a lead becomes real work.</summary>
public static class EmergencyChecklists
{
    public static readonly EmergencyChecklist WebsiteDown = new("website_down", "Website down", new[]
    {
        "DNS resolves", "TLS certificate valid", "HTTP server reachable", "App returns 500/502/503",
        "Recent deployment identified", "Logs available", "Database reachable", "Rollback available"
    });

    public static readonly EmergencyChecklist DotNetIis = new("dotnet_iis", ".NET / IIS issue", new[]
    {
        "App pool status checked", "Event Viewer checked", "stdout logs checked", "web.config checked",
        "connection strings checked", "recent deploy compared", "environment variables checked",
        "database connectivity checked"
    });

    public static readonly EmergencyChecklist Database = new("database", "Database issue", new[]
    {
        "Server reachable", "Credentials valid", "Firewall/IP restrictions checked", "Disk space checked",
        "Locks/deadlocks checked", "Recent migrations checked", "Connection pool exhaustion checked",
        "Backup/rollback options checked"
    });

    public static readonly IReadOnlyList<EmergencyChecklist> All = new[] { WebsiteDown, DotNetIis, Database };

    /// <summary>Picks the most relevant checklist for a problem category.</summary>
    public static EmergencyChecklist SuggestFor(string problemCategory) => problemCategory switch
    {
        "Database Failure" or "Data Loss" => Database,
        "Deployment Failure" => DotNetIis,
        _ => WebsiteDown
    };
}
