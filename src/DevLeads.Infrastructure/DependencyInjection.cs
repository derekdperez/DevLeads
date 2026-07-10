using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using DevLeads.Core;
using DevLeads.Core.Ai;
using DevLeads.Core.Connectors;
using DevLeads.Core.QueryPacks;
using DevLeads.Infrastructure.Ai;
using DevLeads.Infrastructure.Connectors;
using DevLeads.Infrastructure.Data;
using DevLeads.Infrastructure.QueryPacks;
using DevLeads.Infrastructure.Services;
using DevLeads.Infrastructure.Workers;

namespace DevLeads.Infrastructure;

public static class DependencyInjection
{
    /// <summary>Registers the database, connectors, AI providers, domain services, and worker.</summary>
    public static IServiceCollection AddDevLeads(this IServiceCollection services, string connectionString)
    {
        // A factory for short-lived contexts (Blazor components) plus a scoped context
        // resolved from that factory for the domain services and the background worker.
        services.AddDbContextFactory<DevLeadsDbContext>(o => o.UseSqlite(connectionString));
        services.AddScoped<DevLeadsDbContext>(sp =>
            sp.GetRequiredService<IDbContextFactory<DevLeadsDbContext>>().CreateDbContext());

        // Resilient HTTP client shared by connectors.
        services.AddHttpClient(ConnectorSupport.HttpClientName, client =>
        {
            client.Timeout = TimeSpan.FromSeconds(20);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("UrgentLeads.DevLeads/1.0 (local lead discovery; contact: operator)");
            client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        })
        .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
        {
            AutomaticDecompression = DecompressionMethods.All
        });

        // Connectors — read-only sources of real, public business/operator pain.
        services.AddTransient<ISourceConnector, RssConnector>();
        services.AddTransient<ISourceConnector, HackerNewsConnector>();
        services.AddTransient<ISourceConnector, StackExchangeConnector>();
        services.AddTransient<ISourceConnector, RemotiveConnector>();
        services.AddTransient<ISourceConnector, RedditConnector>();
        services.AddTransient<ISourceConnector, OpireConnector>();
        services.AddTransient<ISourceConnector, GitHubSearchConnector>();

        // Query packs + pre-filter (scoped: read from the database).
        services.AddScoped<IQueryPackProvider, DbQueryPackProvider>();
        services.AddScoped<HeuristicPreFilter>();

        // AI providers + router. All decision-making AI goes through the router, which
        // selects a provider by name from settings — OpenCode CLI is the default, with
        // Anthropic and the offline heuristic as switchable alternatives.
        services.AddSingleton<HeuristicTriageProvider>();
        services.AddSingleton<AnthropicTriageProvider>();
        services.AddSingleton<OpenCodeTriageProvider>();
        services.AddSingleton<IAiTriageProvider>(sp => sp.GetRequiredService<OpenCodeTriageProvider>());
        services.AddSingleton<IAiTriageProvider>(sp => sp.GetRequiredService<AnthropicTriageProvider>());
        services.AddSingleton<IAiTriageProvider>(sp => sp.GetRequiredService<HeuristicTriageProvider>());
        services.AddSingleton<AiTriageRouter>();

        // Domain services.
        services.AddScoped<AuditService>();
        services.AddScoped<LeadIngestionService>();
        services.AddScoped<OutreachService>();
        services.AddScoped<QuoteService>();
        services.AddScoped<SourceRunner>();
        services.AddScoped<MaintenanceService>();

        // Background discovery + maintenance loop.
        services.AddHostedService<DiscoveryWorker>();

        return services;
    }

    /// <summary>Creates the database schema and seeds default settings, query packs, and sources.</summary>
    public static async Task InitializeDevLeadsAsync(this IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DevLeadsDbContext>();
        await DatabaseSeeder.InitializeAsync(db);
    }
}
