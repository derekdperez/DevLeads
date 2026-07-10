using DevLeads.Core.Entities;

namespace DevLeads.Core.Ai;

/// <summary>Input to the single-pass triage call.</summary>
public sealed class AiTriageRequest
{
    public string Title { get; set; } = "";
    public string Body { get; set; } = "";
    public string SourceKey { get; set; } = "";
    public DateTimeOffset PostedAt { get; set; }
    public IReadOnlyList<string> MatchedTerms { get; set; } = Array.Empty<string>();
    public decimal HeuristicScore { get; set; }

    /// <summary>Compact operator skill summary ("" when no profile) shown to the model for fit judgment.</summary>
    public string OperatorSkills { get; set; } = "";
}

/// <summary>Outcome of a triage call, including provider metadata for the audit trail.</summary>
public sealed class AiTriageResponse
{
    public bool Succeeded { get; set; }
    public AiTriageResult? Result { get; set; }
    public string Provider { get; set; } = "";
    public string Model { get; set; } = "";
    public string RequestJson { get; set; } = "{}";
    public string? ResponseJson { get; set; }
    public string? ErrorMessage { get; set; }
    public bool Retryable { get; set; }
}

/// <summary>Compact candidate shown to an AI provider before spending a full triage call.</summary>
public sealed class AiShortlistItem
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string Snippet { get; set; } = "";
    public string SourceKey { get; set; } = "";
    public DateTimeOffset PostedAt { get; set; }
    public IReadOnlyList<string> MatchedTerms { get; set; } = Array.Empty<string>();
    public decimal HeuristicScore { get; set; }
}

public sealed class AiShortlistDecision
{
    public string Id { get; set; } = "";
    public bool ShouldTriage { get; set; }
    public string Reason { get; set; } = "";
}

public sealed class AiShortlistResponse
{
    public bool Succeeded { get; set; }
    public IReadOnlyList<AiShortlistDecision> Decisions { get; set; } = Array.Empty<AiShortlistDecision>();
    public string Provider { get; set; } = "";
    public string Model { get; set; } = "";
    public string RequestJson { get; set; } = "{}";
    public string? ResponseJson { get; set; }
    public string? ErrorMessage { get; set; }
    public bool Retryable { get; set; }
}

public interface IAiBatchShortlistProvider
{
    Task<AiShortlistResponse> ShortlistAsync(
        IReadOnlyList<AiShortlistItem> items,
        OperatorSettings settings,
        int maxSelections,
        CancellationToken ct);
}

/// <summary>One item inside a batched triage call, keyed so results map back.</summary>
public sealed class AiBatchTriageItem
{
    public string Id { get; set; } = "";
    public AiTriageRequest Request { get; set; } = new();
}

/// <summary>Outcome of one batched triage call: per-item results keyed by item id.</summary>
public sealed class AiBatchTriageResponse
{
    public bool Succeeded { get; set; }
    public Dictionary<string, AiTriageResult> Results { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public string Provider { get; set; } = "";
    public string Model { get; set; } = "";
    public string RequestJson { get; set; } = "{}";
    public string? ResponseJson { get; set; }
    public string? ErrorMessage { get; set; }
    public bool Retryable { get; set; }
}

/// <summary>
/// Providers that can triage several posts in one model call. Batching is the main AI
/// cost lever: N shortlisted items become ceil(N/chunk) calls instead of N calls.
/// </summary>
public interface IAiBatchTriageProvider
{
    Task<AiBatchTriageResponse> TriageBatchAsync(
        IReadOnlyList<AiBatchTriageItem> items,
        OperatorSettings settings,
        CancellationToken ct);
}

/// <summary>
/// Abstraction over the AI triage backend. Providers are registered by name and selected
/// at runtime through operator settings, so decision-making AI is always switchable.
/// A heuristic implementation lets the app run end-to-end with no AI configured at all.
/// </summary>
public interface IAiTriageProvider
{
    /// <summary>Stable name used in settings to select this provider (e.g. "OpenCode").</summary>
    string Name { get; }

    /// <summary>Whether the provider can currently make calls (CLI present, key set, …).</summary>
    bool IsAvailable(OperatorSettings settings);

    /// <summary>Human-readable explanation when <see cref="IsAvailable"/> is false.</summary>
    string AvailabilityMessage(OperatorSettings settings);

    Task<AiTriageResponse> TriageAsync(AiTriageRequest request, OperatorSettings settings, CancellationToken ct);
}
