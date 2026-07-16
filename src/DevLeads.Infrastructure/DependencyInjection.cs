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
            // Reddit currently rate-limits the longer product-style identifier while this
            // concise, transparent read-only identifier is accepted by its RSS endpoints.
            client.DefaultRequestHeaders.UserAgent.ParseAdd("DevLeads/1.0 (+read-only lead discovery)");
            client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        })
        .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
        {
            AutomaticDecompression = DecompressionMethods.All,
            // Public connector APIs do not need session state. Reddit's edge assigns a
            // sticky rate-limited bucket cookie, so retaining it turns later RSS polls into
            // persistent 429s even while a stateless request succeeds.
            UseCookies = false
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

        // AI providers + routers. Decision-making (triage) AI goes through AiTriageRouter,
        // which selects a provider by name from settings — OpenCode CLI is the default,
        // with Codex (OpenAI), Anthropic, and the offline heuristic as alternatives.
        // Free-text generation goes through AiTextRouter, which picks OpenCode vs Codex
        // per feature. Both CLI providers are registered concretely so the text router
        // and services can inject them directly.
        services.AddSingleton<HeuristicTriageProvider>();
        services.AddSingleton<AnthropicTriageProvider>();
        services.AddSingleton<OpenCodeTriageProvider>();
        services.AddSingleton<CodexCliProvider>();
        services.AddSingleton<IAiTriageProvider>(sp => sp.GetRequiredService<OpenCodeTriageProvider>());
        services.AddSingleton<IAiTriageProvider>(sp => sp.GetRequiredService<CodexCliProvider>());
        services.AddSingleton<IAiTriageProvider>(sp => sp.GetRequiredService<AnthropicTriageProvider>());
        services.AddSingleton<IAiTriageProvider>(sp => sp.GetRequiredService<HeuristicTriageProvider>());
        services.AddSingleton<AiTriageRouter>();
        services.AddSingleton<AiTextRouter>();

        // Live activity feed: singleton so every circuit and the worker share one view.
        services.AddSingleton<DiscoveryActivityTracker>();

        // Domain services.
        services.AddScoped<AuditService>();
        services.AddScoped<LeadIngestionService>();
        services.AddScoped<OutreachService>();
        services.AddScoped<QuoteService>();
        services.AddScoped<SourceRunner>();
        services.AddScoped<MaintenanceService>();
        services.AddScoped<TrendScanService>();
        services.AddScoped<ContentStudioService>();
        services.AddScoped<OperatorPostService>();
        services.AddScoped<ClientService>();
        services.AddScoped<PlatformPresenceService>();
        services.AddScoped<AdvisorService>();
        services.AddScoped<LinkedInService>();
        services.AddScoped<DiscordService>();
        services.AddScoped<WebRescueService>();
        services.AddScoped<EmailService>();
        services.AddScoped<CaseStudyService>();
        services.AddScoped<PortfolioService>();

        // Background discovery + maintenance loop, plus the slow content-trend loop.
        services.AddHostedService<DiscoveryWorker>();
        services.AddHostedService<ContentTrendWorker>();

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
