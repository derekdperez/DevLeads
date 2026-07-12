using DevLeads.Core.Entities;

namespace DevLeads.Core.Skills;

/// <summary>
/// The operator's seeded skill profile (from the operator's own skillset document).
/// Only seeds when the Skills table is empty — the Skills page is the source of truth after that.
/// Weight: 3 = core expertise, 2 = strong, 1 = familiar/context.
/// </summary>
public static class DefaultSkills
{
    private static Skill S(string category, string name, int weight, string aliases = "") =>
        new() { Category = category, Name = name, Weight = weight, Aliases = aliases };

    public static IReadOnlyList<Skill> All { get; } = new[]
    {
        // Primary stack — 20 years .NET full-stack.
        S("Primary stack", "C#", 3, "csharp\nc-sharp"),
        S("Primary stack", ".NET", 3, "dotnet\n.net core\n.net framework"),
        S("Primary stack", "ASP.NET", 3, "asp.net core\naspnet"),
        S("Primary stack", "Blazor", 3, "blazor server\nblazor webassembly\nrazor components"),
        S("Primary stack", "Entity Framework", 3, "ef core\nefcore\nlinq"),
        S("Primary stack", "SQL Server", 3, "mssql\nsqlserver\nt-sql\nstored procedure"),
        // Weight 2 + Backend on purpose: "REST API" appears in virtually every job post,
        // so as a weight-3 "Primary stack" skill it made Go/Python jobs look like core
        // .NET fits. It is a capability, not a stack identity.
        S("Backend", "REST API", 2, "rest apis\nweb api\napi design\napi development"),
        S("Primary stack", "JavaScript", 2, "js\ntypescript"),
        S("Primary stack", "HTML", 2, "css\nhtml/css"),

        S("Backend", "authentication", 2, "authorization\nrole-based access\ntoken-based auth\noauth\njwt"),
        S("Backend", "background services", 2, "worker services\nbackground jobs\nhosted service\njob processing"),
        S("Backend", "legacy system maintenance", 2, "legacy code\nlegacy app"),
        S("Backend", "production debugging", 3, "production outage\nproduction issue\nproduction bug"),
        S("Backend", "performance optimization", 2, "performance tuning\nslow query\nhigh cpu\nmemory leak"),

        S("Frontend", "admin dashboards", 2, "dashboard\ndata grid\ndata grids"),
        S("Frontend", "real-time UI", 1, "signalr\nreal-time updates"),

        S("Databases", "SQLite", 2),
        S("Databases", "PostgreSQL", 2, "postgres"),
        S("Databases", "schema design", 2, "data modeling\nquery optimization\nindexes\nmigration planning"),
        S("Databases", "database troubleshooting", 3, "database connection\nconnection issue\ndatabase down\ndeadlock"),

        S("Architecture", "application architecture", 2, "monolith modernization\nmodular design\nservice decomposition"),
        S("Architecture", "event-driven architecture", 2, "message queue\nrabbitmq"),
        S("Architecture", "multi-tenant", 1, "multitenant\nmulti tenant"),

        S("Cloud & DevOps", "Docker", 2, "container\ndockerfile\ndocker compose"),
        S("Cloud & DevOps", "CI/CD", 2, "github actions\npipeline\ncontinuous integration\ncontinuous deployment"),
        S("Cloud & DevOps", "AWS", 2, "aws ecs\nfargate\nec2\ns3"),
        // Azure lives in "Primary stack": for scoring it is stack identity, not tooling.
        S("Primary stack", "Azure", 3, "azure app service\nazure portal\napp service"),
        S("Cloud & DevOps", "Linux deployment", 2, "linux server\nubuntu server\nsystemd"),
        S("Cloud & DevOps", "Windows deployment", 2, "windows server\niis"),
        S("Cloud & DevOps", "logging & observability", 1, "opentelemetry\napplication logging\nlog analysis"),

        S("AI & automation", "LLM integration", 2, "llm\nopenai api\nanthropic\nprompt engineering\nstructured json output"),
        S("AI & automation", "AI workflow automation", 2, "ai agents\nagent workflows\nai triage\nautomation tools"),

        S("Security & diagnostics", "security triage", 1, "bug bounty\nsecurity findings\nvulnerability triage\ncors\ncookie security\nsubdomain"),
        S("Security & diagnostics", "authentication troubleshooting", 2, "login broken\nauth flow\nusers cannot login"),

        S("Primary stack", ".NET modernization", 3, ".net framework to .net core\nwcf to rest\nwcf migration\nframework migration"),
        S("Specialized", "outage troubleshooting", 3, "critical bug\nunstable system\nemergency fix\nsite down"),
        S("Specialized", "codebase rescue", 2, "technical debt\nundocumented codebase\ncode rescue\nrapid feature"),

        // "Git"/"GitHub" is deliberately absent: it matches every GitHub URL and carries
        // no fit signal — every developer lead touches git.
        S("Tools", "PowerShell", 1, "bash\nshell scripting"),
        S("Tools", "RabbitMQ", 2, "message broker\namqp"),
        S("Tools", "Postman", 1),

        // Project types — used mostly for AI-prompt context and phrase matches in postings.
        S("Project types", "business web applications", 1, "internal admin\nenterprise system"),
        S("Project types", "API backends", 2, "api backend\napi integration"),
        S("Project types", "legacy modernization", 2, "modernization project"),
        S("Project types", "data-driven dashboards", 1, "reporting dashboard"),
        S("Project types", "emergency production fixes", 3, "urgent fix\nhotfix"),
    };
}
