using System.Text.RegularExpressions;
using DevLeads.Core.Entities;

namespace DevLeads.Core.Skills;

/// <summary>A skill that matched a piece of lead text, with its profile weight and category.</summary>
public sealed record SkillMatch(string Name, int Weight, string Category = "");

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
                matches.Add(new SkillMatch(skill.Name, Math.Clamp(skill.Weight, 1, 3), skill.Category));
            }
        }
        return matches;
    }

    /// <summary>
    /// The category whose weight-3 skills are stack *identity* (C#, .NET, Blazor, SQL
    /// Server, Azure, .NET modernization…) rather than transferable capabilities.
    /// Capability phrases like "REST API" or "outage troubleshooting" appear in almost
    /// every technical post and must never make an off-stack post look like core work.
    /// </summary>
    private static readonly string[] StackIdentityCategories = { "Primary stack" };

    /// <summary>True when the text matched at least one weight-3 skill from an identity category.</summary>
    public static bool HasStackIdentityMatch(IEnumerable<SkillMatch> matches) =>
        matches.Any(m => m.Weight >= 3 &&
                         StackIdentityCategories.Contains(m.Category, StringComparer.OrdinalIgnoreCase));

    /// <summary>
    /// Primary-stack demands the operator does NOT serve. Word-boundary patterns keep
    /// noise out ("java" must not hit "javascript"; bare "go" is only counted in
    /// developer/engineer phrasing). A stack drops off this list automatically when the
    /// operator adds a matching enabled skill.
    /// </summary>
    private static readonly (string Name, Regex Pattern)[] ForeignStacks =
    {
        ("Go", new Regex(@"\bgolang\b|\bgo\s+(developer|engineer|dev\b|backend|experience|services)", RegexOptions.IgnoreCase | RegexOptions.Compiled)),
        ("Python", new Regex(@"\bpython\b|\bdjango\b|\bflask\b|\bfastapi\b", RegexOptions.IgnoreCase | RegexOptions.Compiled)),
        ("Java", new Regex(@"\bjava\b(?!\s*script)|\bspring boot\b|\bkotlin\b", RegexOptions.IgnoreCase | RegexOptions.Compiled)),
        ("Ruby", new Regex(@"\bruby\b|\bruby on rails\b", RegexOptions.IgnoreCase | RegexOptions.Compiled)),
        ("PHP", new Regex(@"\bphp\b|\blaravel\b|\bsymfony\b", RegexOptions.IgnoreCase | RegexOptions.Compiled)),
        ("Rust", new Regex(@"\brust\s+(developer|engineer|dev\b|backend|experience)|written in rust", RegexOptions.IgnoreCase | RegexOptions.Compiled)),
        ("Node.js", new Regex(@"\bnode\.?js\b|\bnestjs\b|\bexpress\.js\b", RegexOptions.IgnoreCase | RegexOptions.Compiled)),
        ("React", new Regex(@"\breact\b", RegexOptions.IgnoreCase | RegexOptions.Compiled)),
        ("Vue", new Regex(@"\bvue\.?js?\b", RegexOptions.IgnoreCase | RegexOptions.Compiled)),
        ("Elixir", new Regex(@"\belixir\b", RegexOptions.IgnoreCase | RegexOptions.Compiled)),
        ("Scala", new Regex(@"\bscala\b", RegexOptions.IgnoreCase | RegexOptions.Compiled)),
        ("C++", new Regex(@"c\+\+", RegexOptions.IgnoreCase | RegexOptions.Compiled)),
        ("Mobile", new Regex(@"\bswift\b|\bios developer\b|\bandroid developer\b|\bflutter\b|\breact native\b", RegexOptions.IgnoreCase | RegexOptions.Compiled)),
    };

    /// <summary>
    /// Foreign primary-stack demands found in the text, excluding stacks the operator has
    /// an enabled skill for (adding a "Python" skill makes Python stop being foreign).
    /// </summary>
    public static List<string> ForeignStackDemands(string text, IEnumerable<Skill> skills)
    {
        var owned = skills.Where(s => s.Enabled)
            .SelectMany(s => SplitAliases(s.Aliases).Prepend(s.Name))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return ForeignStacks
            .Where(f => !owned.Contains(f.Name) && f.Pattern.IsMatch(text))
            .Select(f => f.Name)
            .ToList();
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
