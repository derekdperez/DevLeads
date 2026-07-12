using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using DevLeads.Infrastructure.Data;
using DevLeads.Infrastructure.Services;

namespace DevLeads.Infrastructure.Workers;

/// <summary>
/// The core background loop. Every minute it runs any sources that are due (respecting each
/// source's poll interval), and hourly it runs maintenance. This is the "every 5 minutes,
/// DevLeads checks approved public sources…" milestone made real.
/// </summary>
public sealed class DiscoveryWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DiscoveryWorker> _log;
    private DateTimeOffset _lastMaintenance = DateTimeOffset.MinValue;

    public DiscoveryWorker(IServiceScopeFactory scopeFactory, ILogger<DiscoveryWorker> log)
    {
        _scopeFactory = scopeFactory;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Let the app finish starting before the first tick.
        try { await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken); }
        catch (OperationCanceledException) { return; }

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        do
        {
            try { await TickAsync(stoppingToken); }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) { _log.LogError(ex, "Discovery tick failed"); }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task TickAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DevLeadsDbContext>();
        var settings = await db.OperatorSettings.FirstOrDefaultAsync(ct);
        if (settings is null || !settings.DiscoveryEnabled) return;

        var now = DateTimeOffset.UtcNow;
        // Filter the date-based "due" condition in memory — SQLite can't translate the
        // nullable DateTimeOffset null-check-OR-comparison, and the source set is tiny.
        var enabled = await db.SourceConfigs.Where(s => s.Enabled).ToListAsync(ct);
        // A disabled campaign pauses all of its sources without touching their configs.
        var disabledCampaigns = await db.Campaigns.Where(c => !c.Enabled).Select(c => c.Id).ToListAsync(ct);
        var dueSources = enabled.Where(s =>
            (s.NextRunAt == null || s.NextRunAt <= now) &&
            (s.CampaignId == null || !disabledCampaigns.Contains(s.CampaignId.Value))).ToList();

        if (dueSources.Count > 0)
        {
            var runner = scope.ServiceProvider.GetRequiredService<SourceRunner>();
            foreach (var source in dueSources)
            {
                ct.ThrowIfCancellationRequested();
                var created = await runner.RunAsync(source, ct);
                if (created > 0) _log.LogInformation("Source {Source}: {Count} new lead(s)", source.SourceKey, created);
            }
        }

        // Hourly maintenance.
        if (now - _lastMaintenance > TimeSpan.FromHours(1))
        {
            _lastMaintenance = now;
            var maintenance = scope.ServiceProvider.GetRequiredService<MaintenanceService>();
            var vendorSupport = await maintenance.RejectNonHirableVendorSupportAsync(ct);
            var archived = await maintenance.ArchiveStaleLeadsAsync(ct);
            var overdue = await maintenance.FlagOverdueQuotesAsync(ct);
            if (vendorSupport + archived + overdue > 0)
                _log.LogInformation("Maintenance: rejected {VendorSupport} vendor-support leads, archived {Archived} stale, flagged {Overdue} overdue",
                    vendorSupport, archived, overdue);

            // "The next time the AI generation runs": one batched call per hour writes
            // every queued outreach response, budget permitting. The operator can also
            // trigger it any time from the approval queue.
            var outreach = scope.ServiceProvider.GetRequiredService<OutreachService>();
            if (await outreach.QueuedCountAsync(ct) > 0 &&
                !await scope.ServiceProvider.GetRequiredService<LeadIngestionService>()
                    .IsOverAiBudgetAsync(settings, ct))
            {
                var (generated, message) = await outreach.GenerateQueuedResponsesAsync(ct);
                if (generated > 0) _log.LogInformation("Outreach generation: {Message}", message);
            }
        }
    }
}
