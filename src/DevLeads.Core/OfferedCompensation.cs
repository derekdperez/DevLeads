using System.Text.RegularExpressions;

namespace DevLeads.Core;

/// <summary>
/// Extracts a compensation amount the poster explicitly stated ("Reward: $15",
/// "[Bounty $250]", "budget of $500–$800"). Only amounts adjacent to a compensation
/// keyword count — a stray "$120k ARR" in a post body is not an offer. Hourly/periodic
/// rates are skipped (a flat number can't represent them honestly).
/// </summary>
public static class OfferedCompensation
{
    /// <summary>Money token: $1,500 / €250 / $3k (optional k-multiplier).</summary>
    private static readonly Regex Money = new(
        @"[\$€£]\s?(?<val>\d[\d,]*(?:\.\d{1,2})?)\s?(?<k>[kK])?",
        RegexOptions.Compiled);

    private static readonly Regex Keyword = new(
        @"\b(bount(?:y|ies)|reward|budget|prize|compensation|will pay|pays?|paying|offer(?:ing)?|worth)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>Rate suffix directly after the amount — not a flat offer.</summary>
    private static readonly Regex RateSuffix = new(
        @"^\s*(?:/|per\s)\s*(?:hr|hour|day|week|wk|mo|month|yr|year|annum)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>Crypto-denominated amount ("Bounty: 120 XLM", "0.05 ETH reward").</summary>
    private static readonly Regex CryptoMoney = new(
        @"\b(?<val>\d[\d,]*(?:\.\d+)?)\s?(?<token>BTC|ETH|SOL|XLM|XRP|ADA|DOGE|LTC|BNB|AVAX|DOT|TRX|TON|MATIC|POL|USDT|USDC|DAI)\b",
        RegexOptions.Compiled);

    /// <summary>
    /// Deliberately conservative USD per token. Exact rates don't matter here — the point
    /// is that a 120 XLM bounty (~tens of dollars) must not score like a $1,500 engagement.
    /// </summary>
    private static readonly Dictionary<string, double> CryptoUsdRate = new(StringComparer.OrdinalIgnoreCase)
    {
        ["BTC"] = 60000, ["ETH"] = 2000, ["SOL"] = 100, ["BNB"] = 400, ["LTC"] = 60,
        ["AVAX"] = 20, ["DOT"] = 4, ["TON"] = 2, ["XRP"] = 1, ["ADA"] = 0.4,
        ["XLM"] = 0.3, ["DOGE"] = 0.1, ["TRX"] = 0.2, ["MATIC"] = 0.4, ["POL"] = 0.4,
        ["USDT"] = 1, ["USDC"] = 1, ["DAI"] = 1
    };

    /// <summary>Returns the stated amount range, or null when no explicit offer exists.</summary>
    public static (double Min, double Max)? Extract(string title, string body)
    {
        var text = $"{title}\n{body}";
        var amounts = new List<double>();

        foreach (Match m in Money.Matches(text))
        {
            // Skip rates ("$40/hr") — they aren't a flat engagement amount.
            if (RateSuffix.IsMatch(text[(m.Index + m.Length)..Math.Min(text.Length, m.Index + m.Length + 12)]))
                continue;

            // Count only amounts with a compensation keyword within ~40 chars either side.
            var start = Math.Max(0, m.Index - 40);
            var end = Math.Min(text.Length, m.Index + m.Length + 40);
            if (!Keyword.IsMatch(text[start..end])) continue;

            if (!double.TryParse(m.Groups["val"].Value.Replace(",", ""), out var value)) continue;
            if (m.Groups["k"].Success) value *= 1000;
            if (value is < 1 or > 1_000_000) continue; // sanity bounds
            amounts.Add(value);
        }

        foreach (Match m in CryptoMoney.Matches(text))
        {
            var start = Math.Max(0, m.Index - 40);
            var end = Math.Min(text.Length, m.Index + m.Length + 40);
            if (!Keyword.IsMatch(text[start..end])) continue;

            if (!double.TryParse(m.Groups["val"].Value.Replace(",", ""), out var units)) continue;
            var usd = units * CryptoUsdRate[m.Groups["token"].Value];
            if (usd is < 1 or > 1_000_000) continue;
            amounts.Add(usd);
        }

        if (amounts.Count == 0) return null;
        // Multiple amounts near keywords usually form a stated range ("$500–$800 budget").
        return (amounts.Min(), amounts.Max());
    }
}
