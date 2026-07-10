namespace DevLeads.Core;

/// <summary>Outcome of a red-flag scan.</summary>
public sealed record RedFlagResult(bool IsRedFlagged, IReadOnlyList<string> Reasons)
{
    public static readonly RedFlagResult None = new(false, Array.Empty<string>());
}

/// <summary>
/// Flags posts that request unauthorized access, credential theft, malware, fraud,
/// or otherwise carry ownership/authorization risk. Such posts must never be auto-contacted.
/// </summary>
public static class RedFlagDetector
{
    private static readonly (string Phrase, string Reason)[] Patterns =
    {
        ("without authorization", "Requests access without authorization"),
        ("without permission", "Requests access without permission"),
        ("bypass login", "Requests bypassing login"),
        ("bypass the login", "Requests bypassing login"),
        ("bypass authentication", "Requests bypassing authentication"),
        ("crack the password", "Requests cracking credentials"),
        ("crack password", "Requests cracking credentials"),
        ("recover password without", "Suspicious credential recovery"),
        ("steal", "Mentions stealing"),
        ("hack into", "Requests unauthorized access"),
        ("hack an account", "Requests unauthorized access"),
        ("brute force", "Credential brute-forcing"),
        ("malware", "Involves malware"),
        ("ransomware", "Involves ransomware"),
        ("keylogger", "Involves surveillance malware"),
        ("ddos", "Involves attack tooling"),
        ("phishing", "Involves fraud/phishing"),
        ("carding", "Involves payment fraud"),
        ("not my site but", "Unclear system ownership"),
        ("someone else's account", "Unclear system ownership"),
        ("extract personal data", "Personal data extraction"),
        ("scrape personal", "Personal data extraction"),
        ("cloaker", "Ad-network policy evasion (cloaking)"),
        ("cloaking", "Ad-network policy evasion (cloaking)"),
        ("avoid getting banned", "Platform ban evasion"),
        ("avoid being banned", "Platform ban evasion"),
        ("without getting banned", "Platform ban evasion"),
        ("get around the ban", "Platform ban evasion"),
        ("bypass the ban", "Platform ban evasion"),
        ("evade detection", "Detection evasion"),
        ("bypass detection", "Detection evasion"),
        ("stealth account", "Platform ban evasion"),
        ("aged accounts", "Account fraud"),
        ("fake reviews", "Review fraud"),
        ("fake traffic", "Traffic fraud"),
    };

    public static RedFlagResult Scan(string title, string body)
    {
        var text = $"{title}\n{body}".ToLowerInvariant();
        var reasons = new List<string>();
        foreach (var (phrase, reason) in Patterns)
        {
            if (text.Contains(phrase) && !reasons.Contains(reason))
                reasons.Add(reason);
        }
        return reasons.Count == 0 ? RedFlagResult.None : new RedFlagResult(true, reasons);
    }
}
