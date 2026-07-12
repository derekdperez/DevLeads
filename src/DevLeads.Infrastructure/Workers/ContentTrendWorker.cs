using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using DevLeads.Core;
using DevLeads.Infrastructure.Data;
using DevLeads.Infrastructure.Services;

namespace DevLeads.Infrastructure.Workers;

/// <summary>
/// Slow background loop for the content studio: polls due trend sources (default twice a
/// day per source) and, at most once a day, spends one AI call turning fresh signals into
/// topic suggestions. Draft writing stays operator-initiated.
/// </summary>
public sealed class ContentTrendWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ContentTrendWorker> _log;

    public ContentTrendWorker(IServiceScopeFactory scopeFactory, ILogger<ContentTrendWorker> log)
    {
        _scopeFactory = scopeFactory;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Stagger away from DiscoveryWorker's startup burst.
        try { await Task.Delay(TimeSpan.FromSeconds(45), stoppingToken); }
        catch (OperationCanceledException) { return; }

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(5));
        do
        {
            try { await TickAsync(stoppingToken); }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) { _log.LogError(ex, "Content trend tick failed"); }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task TickAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DevLeadsDbContext>();
        var settings = await db.OperatorSettings.AsNoTracking().FirstOrDefaultAsync(ct);
        if (settings is null || !settings.ContentDiscoveryEnabled) return;

        var scanner = scope.ServiceProvider.GetRequiredService<TrendScanService>();
        var created = await scanner.RunDueAsync(force: false, ct);
        if (created > 0)
            _log.LogInformation("Trend scan: {Count} new signal(s).", created);

        await MaybeSuggestTopicsAsync(scope.ServiceProvider, db, ct);
    }

    /// <summary>
    /// One automatic topic-suggestion call per day, and only when there is fresh material
    /// to work with — keeps the AI budget impact negligible.
    /// </summary>
    private async Task MaybeSuggestTopicsAsync(IServiceProvider services, DevLeadsDbContext db, CancellationToken ct)
    {
        var dayAgo = DateTimeOffset.UtcNow.AddHours(-24);
        if (await db.ContentTopics.AnyAsync(t => t.CreatedAt >= dayAgo, ct)) return;

        var freshCutoff = DateTimeOffset.UtcNow.AddHours(-48);
        var freshSignals = await db.TrendSignals.CountAsync(s => s.FetchedAt >= freshCutoff, ct);
        if (freshSignals < 6) return;

        var studio = services.GetRequiredService<ContentStudioService>();
        var (createdTopics, message) = await studio.GenerateTopicsAsync(4, ct);
        _log.LogInformation("Auto topic suggestion: {Message}", message);
    }
}
