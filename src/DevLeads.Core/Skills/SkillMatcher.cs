using DevLeads.Core.Entities;

namespace DevLeads.Core.Skills;

/// <summary>A skill that matched a piece of lead text, with its profile weight.</summary>
public sealed record SkillMatch(string Name, int Weight);

/// <summary>Matches lead text against the operator's skill profile and scores the fit.</summary>
public static class SkillMatcher
{
    /// <summary>All enabled skills whose name or any alias appears in the text (case-insensitive).</summary>
    public static List<SkillMatch> Match(string text, IEnumerable<Skill> skills)
    {
        var matches = new List<SkillMatch>();
        foreach (var skill in skills)
        {
            if (!skill.Enabled) continue;
            if (ContainsTerm(text, skill.Name) ||
                SplitAliases(skill.Aliases).Any(a => ContainsTerm(text, a)))
            {
                matches.Add(new SkillMatch(skill.Name, Math.Clamp(skill.Weight, 1, 3)));
            }
        }
        return matches;
    }

    /// <summary>
    /// 0–100 fit score mirroring the legacy stack tiers: a core-skill match scores like the
    /// preferred stack, strong like the secondary stack; breadth adds a small bonus.
    /// </summary>
    public static double FitScore(IReadOnlyList<SkillMatch> matches)
    {
        if (matches.Count == 0) return 35; // unknown fit, same as the legacy fallback
        var best = matches.Max(m => m.Weight);
        double score = best switch { 3 => 90, 2 => 72, _ => 55 };
        score += Math.Min(matches.Count - 1, 5) * 2; // breadth bonus, capped
        return Math.Clamp(score, 0, 100);
    }

    /// <summary>Compact profile description for the AI triage prompt, strongest skills first.</summary>
    public static string PromptSummary(IEnumerable<Skill> skills, int maxItems = 40)
    {
        var names = skills.Where(s => s.Enabled)
            .OrderByDescending(s => s.Weight)
            .Take(maxItems)
            .Select(s => s.Weight >= 3 ? s.Name + " (core)" : s.Name);
        return string.Join(", ", names);
    }

    /// <summary>Search keywords for connectors (bounty/issue queries): short, high-weight names first.</summary>
    public static List<string> SearchTerms(IEnumerable<Skill> skills, int max = 12) =>
        skills.Where(s => s.Enabled && s.Name.Length <= 24)
            .OrderByDescending(s => s.Weight)
            .ThenBy(s => s.Name.Length)
            .Select(s => s.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(max)
            .ToList();

    private static bool ContainsTerm(string text, string term) =>
        !string.IsNullOrWhiteSpace(term) && text.Contains(term, StringComparison.OrdinalIgnoreCase);

    private static IEnumerable<string> SplitAliases(string aliases) =>
        string.IsNullOrWhiteSpace(aliases)
            ? Array.Empty<string>()
            : aliases.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
