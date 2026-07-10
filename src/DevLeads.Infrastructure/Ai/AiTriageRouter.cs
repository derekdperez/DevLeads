using Microsoft.Extensions.Logging;
using DevLeads.Core.Ai;
using DevLeads.Core.Entities;

namespace DevLeads.Infrastructure.Ai;

/// <summary>
/// Registry of AI triage providers, selected by name from operator settings. All
/// decision-making AI flows through here, so the backend is always switchable:
/// OpenCode CLI (default) → Anthropic API → Heuristic rules (final fallback, always on).
/// Applies the simple retry/backoff policy for the single triage call.
/// </summary>
public sealed class AiTriageRouter
{
    /// <summary>Items per batched triage call — one model call covers this many leads.</summary>
    public const int BatchTriageChunkSize = 6;

    private readonly IReadOnlyList<IAiTriageProvider> _providers;
    private readonly HeuristicTriageProvider _heuristic;
    private readonly ILogger<AiTriageRouter> _log;

    public AiTriageRouter(IEnumerable<IAiTriageProvider> providers, HeuristicTriageProvider heuristic, ILogger<AiTriageRouter> log)
    {
        _providers = providers.ToList();
        _heuristic = heuristic;
        _log = log;
    }

    public IReadOnlyList<IAiTriageProvider> Providers => _providers;

    /// <summary>The provider that will actually serve calls for these settings.</summary>
    public IAiTriageProvider Resolve(OperatorSettings settings)
    {
        var selected = _providers.FirstOrDefault(p =>
            p.Name.Equals(settings.AiProvider, StringComparison.OrdinalIgnoreCase));

        if (selected is null)
        {
            _log.LogWarning("Unknown AI provider '{Provider}' — using Heuristic.", settings.AiProvider);
            return _heuristic;
        }

        if (!selected.IsAvailable(settings))
        {
            _log.LogWarning("AI provider '{Provider}' unavailable ({Reason}) — using Heuristic.",
                selected.Name, selected.AvailabilityMessage(settings));
            return _heuristic;
        }

        return selected;
    }

    public async Task<AiTriageResponse> TriageAsync(AiTriageRequest request, OperatorSettings settings, CancellationToken ct)
    {
        var provider = Resolve(settings);
        if (ReferenceEquals(provider, _heuristic))
            return await _heuristic.TriageAsync(request, settings, ct);

        var attempts = Math.Max(1, settings.AiRetryCount);
        AiTriageResponse last = new() { Succeeded = false, ErrorMessage = "not attempted" };

        for (var attempt = 1; attempt <= attempts; attempt++)
        {
            last = await provider.TriageAsync(request, settings, ct);
            if (last.Succeeded) return last;
            if (!last.Retryable) break;

            _log.LogWarning("AI triage attempt {Attempt}/{Max} via {Provider} failed (retryable): {Error}",
                attempt, attempts, provider.Name, last.ErrorMessage);
            if (attempt < attempts)
                await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)), ct);
        }

        return last;
    }

    /// <summary>
    /// Triages several posts in one model call when the resolved provider supports it.
    /// Returns a failed response when it doesn't — callers then use per-item triage.
    /// </summary>
    public async Task<AiBatchTriageResponse> TriageBatchAsync(
        IReadOnlyList<AiBatchTriageItem> items,
        OperatorSettings settings,
        CancellationToken ct)
    {
        var provider = Resolve(settings);
        if (provider is not IAiBatchTriageProvider batchProvider || ReferenceEquals(provider, _heuristic))
            return new AiBatchTriageResponse { Succeeded = false, ErrorMessage = "Provider does not support batch triage." };

        var attempts = Math.Max(1, settings.AiRetryCount);
        AiBatchTriageResponse last = new() { Succeeded = false, ErrorMessage = "not attempted" };

        for (var attempt = 1; attempt <= attempts; attempt++)
        {
            last = await batchProvider.TriageBatchAsync(items, settings, ct);
            if (last.Succeeded) return last;
            if (!last.Retryable) break;

            _log.LogWarning("AI batch triage attempt {Attempt}/{Max} via {Provider} failed (retryable): {Error}",
                attempt, attempts, provider.Name, last.ErrorMessage);
            if (attempt < attempts)
                await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)), ct);
        }

        return last;
    }

    public async Task<AiShortlistResponse> ShortlistAsync(
        IReadOnlyList<AiShortlistItem> items,
        OperatorSettings settings,
        int maxSelections,
        CancellationToken ct)
    {
        var provider = Resolve(settings);
        var boundedMax = Math.Clamp(maxSelections, 0, items.Count);
        if (provider is IAiBatchShortlistProvider shortlistProvider && !ReferenceEquals(provider, _heuristic))
        {
            var attempts = Math.Max(1, settings.AiRetryCount);
            AiShortlistResponse last = new() { Succeeded = false, ErrorMessage = "not attempted" };

            for (var attempt = 1; attempt <= attempts; attempt++)
            {
                last = await shortlistProvider.ShortlistAsync(items, settings, boundedMax, ct);
                if (last.Succeeded) return last;
                if (!last.Retryable) break;

                _log.LogWarning("AI shortlist attempt {Attempt}/{Max} via {Provider} failed (retryable): {Error}",
                    attempt, attempts, provider.Name, last.ErrorMessage);
                if (attempt < attempts)
                    await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)), ct);
            }

            _log.LogWarning("AI shortlist via {Provider} failed ({Error}); using heuristic top {MaxSelections}.",
                provider.Name, last.ErrorMessage, boundedMax);
        }

        return BuildHeuristicShortlist(items, boundedMax);
    }

    private static AiShortlistResponse BuildHeuristicShortlist(IReadOnlyList<AiShortlistItem> items, int maxSelections)
    {
        var selected = items
            .OrderByDescending(i => i.HeuristicScore)
            .ThenByDescending(i => i.MatchedTerms.Count)
            .Take(maxSelections)
            .Select(i => i.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return new AiShortlistResponse
        {
            Succeeded = true,
            Provider = "Heuristic",
            Model = "shortlist-rules",
            Decisions = items.Select(i => new AiShortlistDecision
            {
                Id = i.Id,
                ShouldTriage = selected.Contains(i.Id),
                Reason = selected.Contains(i.Id) ? "Top heuristic candidate." : "Below shortlist cutoff."
            }).ToList()
        };
    }
}
