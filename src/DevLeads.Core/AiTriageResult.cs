using System.Text.Json.Serialization;

namespace DevLeads.Core;

/// <summary>
/// The strict structured object returned by the single-pass AI triage call.
/// Satisfies all former pipeline stages (relevance, emergency, category, stack,
/// cause, first step, fix time, confidence, recommendation) at once.
/// </summary>
public sealed class AiTriageResult
{
    /// <summary>Predominant natural language of the original post as an ISO 639-1 code.</summary>
    [JsonPropertyName("languageCode")]
    public string LanguageCode { get; set; } = "en";

    /// <summary>Faithful English title when the post is non-English; empty for English posts.</summary>
    [JsonPropertyName("englishTitle")]
    public string EnglishTitle { get; set; } = "";

    /// <summary>Faithful English body when the post is non-English; empty for English posts.</summary>
    [JsonPropertyName("englishBody")]
    public string EnglishBody { get; set; } = "";

    [JsonPropertyName("isTechnicalProblem")]
    public bool IsTechnicalProblem { get; set; }

    [JsonPropertyName("isEmergency")]
    public bool IsEmergency { get; set; }

    /// <summary>
    /// Willingness-to-pay judgment: "Explicit" (mentions hiring/paying/budget),
    /// "Implied" (commercial impact suggests they'd pay), "None" (free-advice seeker).
    /// Empty string means the provider/response predates this field — treated as unknown.
    /// </summary>
    [JsonPropertyName("paymentIntent")]
    public string PaymentIntent { get; set; } = "";

    /// <summary>
    /// True when the poster wants someone to actually do the work (hire, fix, implement);
    /// false when they only want information, an explanation, or a recommendation.
    /// </summary>
    [JsonPropertyName("assistanceRequested")]
    public bool AssistanceRequested { get; set; }

    [JsonPropertyName("rejectReason")]
    public string? RejectReason { get; set; }

    [JsonPropertyName("problemCategory")]
    public string ProblemCategory { get; set; } = "";

    [JsonPropertyName("detectedStack")]
    public List<string> DetectedStack { get; set; } = new();

    [JsonPropertyName("estimatedCause")]
    public string EstimatedCause { get; set; } = "";

    [JsonPropertyName("firstDiagnosticStep")]
    public string FirstDiagnosticStep { get; set; } = "";

    [JsonPropertyName("estimatedFixMinutesMin")]
    public int? EstimatedFixMinutesMin { get; set; }

    [JsonPropertyName("estimatedFixMinutesMax")]
    public int? EstimatedFixMinutesMax { get; set; }

    [JsonPropertyName("aiConfidence")]
    public decimal AiConfidence { get; set; }

    [JsonPropertyName("outreachRecommendation")]
    public string OutreachRecommendation { get; set; } = "Manual Review";

    /// <summary>Valid problem categories from the strict schema.</summary>
    public static readonly string[] ProblemCategories =
    {
        "Production Outage", "Website Down", "Database Failure", "Deployment Failure",
        "API Failure", "Authentication/Login Failure", "Payment/Checkout Failure",
        "DNS/TLS Failure", "Performance Emergency", "Data Loss", "Security Incident",
        "Feature Request", "Modernization/Migration", "Non-Urgent Help Request", "Not Relevant"
    };

    /// <summary>Valid payment-intent values from the strict schema.</summary>
    public static readonly string[] PaymentIntents = { "Explicit", "Implied", "None" };

    /// <summary>Valid outreach recommendations from the strict schema.</summary>
    public static readonly string[] OutreachRecommendations =
    {
        "Ignore", "Watch", "Manual Review", "Draft Reply", "Do Not Contact"
    };
}
