using System.Text.Json;
using DevLeads.Core;
using DevLeads.Core.Entities;

namespace DevLeads.Web.Components.Shared;

/// <summary>Presentation helpers: badge classes, labels, and formatting used across pages.</summary>
public static class UiHelpers
{
    public static string PriorityClass(Priority p) => p switch
    {
        Priority.Critical => "rr-badge crit",
        Priority.High => "rr-badge high",
        Priority.Medium => "rr-badge med",
        Priority.Watch => "rr-badge watch",
        _ => "rr-badge low"
    };

    public static string StatusClass(OpportunityStatus s) => s switch
    {
        OpportunityStatus.DraftReady or OpportunityStatus.Approved => "rr-chip active",
        OpportunityStatus.Contacted or OpportunityStatus.Responded or OpportunityStatus.Qualified => "rr-chip progress",
        OpportunityStatus.QuoteDrafted or OpportunityStatus.QuoteSent or OpportunityStatus.Accepted => "rr-chip quote",
        OpportunityStatus.InProgress or OpportunityStatus.Fixed or OpportunityStatus.PaymentPending => "rr-chip work",
        OpportunityStatus.Paid or OpportunityStatus.Won => "rr-chip won",
        OpportunityStatus.Rejected or OpportunityStatus.PreFilteredRejected or OpportunityStatus.Lost
            or OpportunityStatus.DoNotContact or OpportunityStatus.Archived => "rr-chip dead",
        OpportunityStatus.NeedsReview => "rr-chip review",
        _ => "rr-chip"
    };

    public static string AiStatusClass(AiJobStatus s) => s switch
    {
        AiJobStatus.Succeeded => "rr-chip won",
        AiJobStatus.NeedsManualReview => "rr-chip review",
        AiJobStatus.FailedFinal or AiJobStatus.FailedRetryable => "rr-chip dead",
        AiJobStatus.Running or AiJobStatus.Queued => "rr-chip progress",
        _ => "rr-chip"
    };

    public static string Spaced(Enum e)
    {
        var s = e.ToString();
        return System.Text.RegularExpressions.Regex.Replace(s, "(?<=[a-z])(?=[A-Z])", " ");
    }

    public static string Age(DateTimeOffset from)
    {
        var d = DateTimeOffset.UtcNow - from;
        if (d.TotalMinutes < 1) return "just now";
        if (d.TotalMinutes < 60) return $"{(int)d.TotalMinutes}m ago";
        if (d.TotalHours < 24) return $"{(int)d.TotalHours}h ago";
        return $"{(int)d.TotalDays}d ago";
    }

    /// <summary>Freshness badge: green &lt; 1 day, yellow 1–3 days, red 3+ days.</summary>
    public static string AgeClass(DateTimeOffset from)
    {
        var days = (DateTimeOffset.UtcNow - from).TotalDays;
        return days < 1 ? "rr-chip age-fresh" : days < 3 ? "rr-chip age-mid" : "rr-chip age-old";
    }

    /// <summary>
    /// Did the author indicate they'd pay someone? True on an explicit pay-intent verdict or
    /// explicit pay language in the post; false when triage judged intent Implied/None;
    /// null when the lead hasn't been triaged for it yet.
    /// </summary>
    public static bool? CompensationOffered(Opportunity o)
    {
        if (o.PaymentIntent == "Explicit") return true;
        if (ParseStringList(o.MatchedTermsJson).Any(t => t.StartsWith("pay:", StringComparison.OrdinalIgnoreCase))) return true;
        return o.PaymentIntent is "Implied" or "None" ? false : null;
    }

    /// <summary>Yes/No/— chip for tri-state judgments.</summary>
    public static (string Css, string Label) YesNo(bool? value) => value switch
    {
        true => ("rr-chip won", "Yes"),
        false => ("rr-chip dead", "No"),
        null => ("rr-chip", "—")
    };

    public static string Fee(double? min, double? max) =>
        min is null && max is null ? "—" : $"${min:0}–${max:0}";

    /// <summary>
    /// Fee with provenance: an amount the poster stated is fact ("$15 offered"); a
    /// category-based suggestion is clearly marked as an estimate ("~$100–$250 est.").
    /// </summary>
    public static string Fee(Opportunity o)
    {
        if (o.EstimatedFeeMin is null && o.EstimatedFeeMax is null) return "—";
        if (!o.FeeIsEstimate)
            return o.EstimatedFeeMin == o.EstimatedFeeMax
                ? $"${o.EstimatedFeeMin:0} offered"
                : $"${o.EstimatedFeeMin:0}–${o.EstimatedFeeMax:0} offered";
        return $"~${o.EstimatedFeeMin:0}–${o.EstimatedFeeMax:0} est.";
    }

    public static List<string> ParseStringList(string json)
    {
        try { return JsonSerializer.Deserialize<List<string>>(json) ?? new(); }
        catch { return new(); }
    }
}
