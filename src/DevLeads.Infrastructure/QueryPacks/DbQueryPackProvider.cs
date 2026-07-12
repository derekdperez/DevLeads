using DevLeads.Core.QueryPacks;
using DevLeads.Infrastructure.Data;

namespace DevLeads.Infrastructure.QueryPacks;

/// <summary>Loads query-pack terms from the database (cached per scope).</summary>
public sealed class DbQueryPackProvider : IQueryPackProvider
{
    private readonly DevLeadsDbContext _db;
    private List<Core.Entities.QueryPack>? _cache;

    public DbQueryPackProvider(DevLeadsDbContext db) => _db = db;

    private List<Core.Entities.QueryPack> Packs => _cache ??= _db.QueryPacks.ToList();

    private static string[] Split(string terms) =>
        terms.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    public IReadOnlyList<string> GetTerms(string packName)
    {
        var pack = Packs.FirstOrDefault(p => string.Equals(p.Name, packName, StringComparison.OrdinalIgnoreCase));
        return pack is null ? Array.Empty<string>() : Split(pack.Terms);
    }

    public IReadOnlyList<string> GetHighPriorityTerms() =>
        Packs.Where(p => p.IsHighPriority).SelectMany(p => Split(p.Terms))
             .Distinct(StringComparer.OrdinalIgnoreCase).ToList();

    public IReadOnlyList<string> GetHighPriorityTerms(IReadOnlyCollection<string> packNames) =>
        Packs.Where(p => p.IsHighPriority &&
                         packNames.Contains(p.Name, StringComparer.OrdinalIgnoreCase))
             .SelectMany(p => Split(p.Terms))
             .Distinct(StringComparer.OrdinalIgnoreCase).ToList();

    public IReadOnlyList<string> GetNegativeTerms() =>
        Packs.Where(p => p.IsNegative).SelectMany(p => Split(p.Terms))
             .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
}
