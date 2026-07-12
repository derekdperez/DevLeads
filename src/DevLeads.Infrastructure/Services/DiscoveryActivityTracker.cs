namespace DevLeads.Infrastructure.Services;

/// <summary>
/// In-memory, app-wide record of what discovery is doing right now: which sources are
/// mid-fetch and a rolling feed of recent events (runs, new leads, failures). Singleton;
/// SourceRunner reports into it and the ActivityFeed UI polls it.
/// </summary>
public sealed class DiscoveryActivityTracker
{
    public sealed record ActivityEvent(DateTimeOffset At, string Kind, string SourceKey, string Message);
    public sealed record RunningSource(string SourceKey, string DisplayName, DateTimeOffset StartedAt);

    private const int MaxEvents = 80;
    private readonly object _gate = new();
    private readonly LinkedList<ActivityEvent> _events = new();
    private readonly Dictionary<string, RunningSource> _running = new(StringComparer.OrdinalIgnoreCase);

    public void RunStarted(string sourceKey, string displayName)
    {
        lock (_gate)
        {
            _running[sourceKey] = new RunningSource(sourceKey, displayName, DateTimeOffset.UtcNow);
            AddLocked("run-start", sourceKey, $"Fetching {displayName}…");
        }
    }

    public void RunCompleted(string sourceKey, bool healthy, string message)
    {
        lock (_gate)
        {
            _running.Remove(sourceKey);
            AddLocked(healthy ? "run-ok" : "run-fail", sourceKey, message);
        }
    }

    public void LeadCreated(string sourceKey, string title, double score)
    {
        lock (_gate)
            AddLocked("lead", sourceKey, $"New lead (score {score:0}): {title}");
    }

    public (IReadOnlyList<RunningSource> Running, IReadOnlyList<ActivityEvent> Events) Snapshot()
    {
        lock (_gate)
            return (_running.Values.OrderBy(r => r.StartedAt).ToList(), _events.ToList());
    }

    private void AddLocked(string kind, string sourceKey, string message)
    {
        _events.AddFirst(new ActivityEvent(DateTimeOffset.UtcNow, kind, sourceKey, message));
        while (_events.Count > MaxEvents) _events.RemoveLast();
    }
}
