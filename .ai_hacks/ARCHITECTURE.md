# Architecture and Navigation Guide

A solo-operator Blazor application that discovers public paid software-work leads, triages and scores them, manages human-approved outreach and quotes, turns trend signals into content drafts, and manages an approved LinkedIn publishing and engagement workflow.

## Project dependency direction

```text
DevLeads.Web  ->  DevLeads.Infrastructure  ->  DevLeads.Core
```

- **DevLeads.Core** (`src/DevLeads.Core`) — Domain entities, enums, connector and AI contracts, lead-quality rules, scoring, skill matching, prompts, query-pack defaults, and templates; no infrastructure dependencies.
- **DevLeads.Infrastructure** (`src/DevLeads.Infrastructure`) — SQLite persistence, database initialization, source connectors, AI providers, application services, and recurring discovery/content workers.
- **DevLeads.Web** (`src/DevLeads.Web`) — Blazor UI, internal minimal API, application composition, static assets, and supervised process restart.

## Change routing

| Concern | Start here | Also check / warning |
| --- | --- | --- |
| Application startup, service lifetime, or composition | `src/DevLeads.Web/Program.cs`, `DevLeads.Infrastructure.DependencyInjection.AddDevLeads` | `DevLeads.Infrastructure.DependencyInjection.InitializeDevLeadsAsync`, `src/DevLeads.Web/DevLeads.Web.csproj` |
| Entity, relationship, SQLite schema, or persisted enum change | `src/DevLeads.Core/Entities`, `DevLeads.Infrastructure.Data.DevLeadsDbContext` | `DevLeads.Infrastructure.Data.DatabaseSeeder.ApplySchemaUpgradesAsync`, `src/DevLeads.Core/Enums.cs`, `src/DevLeads.Web/Api/ApiEndpoints.cs` Warning: There are no EF migration files; existing databases are upgraded by idempotent SQL in DatabaseSeeder. |
| Seeded campaign, source, query-pack, skill, or trend-source defaults | `DevLeads.Infrastructure.Data.DatabaseSeeder`, `DevLeads.Core.QueryPacks.DefaultQueryPacks`, `DevLeads.Core.Skills.DefaultSkills` | `DevLeads.Infrastructure.QueryPacks.DbQueryPackProvider`, `src/DevLeads.Web/Components/Pages/Sources.razor`, `src/DevLeads.Web/Components/Pages/Campaigns.razor` Warning: Changing recognized seeded source defaults can purge untouched discovery leads and dedup history so they are re-ingested under the new configuration. |
| Connector fetch behavior or source-specific parameters | `DevLeads.Core.Connectors.ISourceConnector`, `src/DevLeads.Infrastructure/Connectors`, `DevLeads.Infrastructure.Services.SourceRunner.RunAsync` | `DevLeads.Infrastructure.Data.DatabaseSeeder.EmergencySources`, `DevLeads.Infrastructure.Data.DatabaseSeeder.ModernizationSources`, `DevLeads.Core.Entities.SourceConfig` |
| Lead quality, false positives, pay intent, rejection, or duplicate behavior | `DevLeads.Core.HeuristicPreFilter.Analyze`, `DevLeads.Core.LeadQualityRules`, `DevLeads.Infrastructure.Services.LeadIngestionService.DecideStatusAndDraft` | `DevLeads.Core.Scoring.OpportunityScorer.Score`, `DevLeads.Core.Skills.SkillMatcher`, `DevLeads.Infrastructure.Services.MaintenanceService`, `DevLeads.Infrastructure.Data.DatabaseSeeder` Warning: Quality gates are deliberately duplicated at prefilter, triage decision, scoring, maintenance, and startup cleanup boundaries; keep them consistent. |
| Score, priority, fee, or operator-fit behavior | `DevLeads.Core.Scoring.OpportunityScorer.Score`, `DevLeads.Core.Skills.SkillMatcher`, `DevLeads.Core.OfferedCompensation.Extract` | `DevLeads.Infrastructure.Services.LeadIngestionService.RunTriageScoreAndDraftAsync`, `DevLeads.Core.Templates.PricingTiers`, `DevLeads.Core.Entities.SourceConfig` |
| AI triage schema, prompt, provider, model, retry, or budget behavior | `src/DevLeads.Core/Ai/IAiTriageProvider.cs`, `src/DevLeads.Core/AiTriageResult.cs`, `src/DevLeads.Core/Ai/AiTriagePrompts.cs`, `DevLeads.Infrastructure.Ai.AiTriageRouter` | `src/DevLeads.Infrastructure/Ai`, `DevLeads.Infrastructure.Services.SourceRunner.BatchTriageAsync`, `DevLeads.Infrastructure.Services.LeadIngestionService.IsOverAiBudgetAsync`, `src/DevLeads.Web/Components/Pages/Settings.razor` |
| Outreach generation, approval, sending, follow-up, or suppression | `DevLeads.Infrastructure.Services.OutreachService`, `src/DevLeads.Core/Ai/OutreachPrompts.cs` | `DevLeads.Infrastructure.Services.LeadIngestionService.CreateDraft`, `src/DevLeads.Web/Components/Pages/Drafts.razor`, `src/DevLeads.Web/Components/Pages/OpportunityDetail.razor`, `src/DevLeads.Web/Api/ApiEndpoints.cs` Warning: Several outreach settings are persisted but not currently enforced; verify usages before assuming a UI setting changes runtime behavior. |
| Content trend source, topic, or draft behavior | `DevLeads.Infrastructure.Services.TrendScanService`, `DevLeads.Infrastructure.Services.ContentStudioService`, `src/DevLeads.Core/Ai/ContentPrompts.cs` | `DevLeads.Infrastructure.Workers.ContentTrendWorker`, `DevLeads.Infrastructure.Data.DatabaseSeeder.SeedTrendSourcesAsync`, `src/DevLeads.Web/Components/Pages/Content.razor` |
| My Posts metrics, Reddit sync, inbox messages, or read state | `DevLeads.Infrastructure.Services.OperatorPostService`, `src/DevLeads.Web/Components/Pages/MyPosts.razor` | `src/DevLeads.Core/Entities/OperatorPost.cs`, `src/DevLeads.Core/Entities/OperatorMessage.cs`, `src/DevLeads.Web/Components/Shared/PostPerformanceChart.razor`, `src/DevLeads.Web/Api/ApiEndpoints.cs` Warning: Reddit can omit view_count even for authenticated requests; preserve the difference between an observed zero and an unavailable count. Opening an unread message here must persist Read immediately. |
| LinkedIn OAuth, scheduled publishing, comment monitoring, or engagement replies | `DevLeads.Infrastructure.Services.LinkedInService`, `src/DevLeads.Web/Components/Pages/LinkedIn.razor` | `src/DevLeads.Core/Ai/LinkedInPrompts.cs`, `src/DevLeads.Core/Entities/EngagementDraft.cs`, `src/DevLeads.Core/Entities/OperatorPost.cs`, `src/DevLeads.Core/Entities/OperatorSettings.cs`, `DevLeads.Infrastructure.Workers.DiscoveryWorker.TickAsync`, `DevLeads.Infrastructure.Services.AdvisorService.BuildAgendaAsync`, `src/DevLeads.Web/Api/ApiEndpoints.cs` Warning: Never treat a self-serve posting token as permission to read comments or private messages. OAuth callbacks must validate the short-lived state, scheduled publishing and replies must honor the global kill switch, and AI responses remain human-reviewed. |
| Blazor route or page behavior | `src/DevLeads.Web/Components/Pages`, `src/DevLeads.Web/Components/Routes.razor` | `src/DevLeads.Web/Components/Shared`, `src/DevLeads.Web/Components/Layout`, `src/DevLeads.Web/wwwroot/app.css` |
| Automation API route or JSON contract | `DevLeads.Web.Api.ApiEndpoints.MapDevLeadsApi` | `src/DevLeads.Web/Program.cs`, `src/DevLeads.Core/Entities`, `src/DevLeads.Infrastructure/Services` Warning: The /api group disables antiforgery and currently has no authentication or authorization policy. |
| Static web asset, reconnect, MIME, or restart behavior | `src/DevLeads.Web/DevLeads.Web.csproj`, `src/DevLeads.Web/Components/App.razor`, `src/DevLeads.Web/Components/Layout/ReconnectModal.razor`, `DevLeads.Web.AppRestartService.Restart` | `src/DevLeads.Web/Program.cs`, `src/DevLeads.Web/Components/Layout/ReconnectModal.razor.js` Warning: The project materializes selected framework, CSS, and component-module assets after build; validate served URLs and MIME types, not only compilation. |
| Generated project navigation context | `ai_hacks.context.json`, `ai_hacks.py` | `.ai_hacks/AI_CONTEXT_COMPACT.md`, `.ai_hacks/PUBLIC_API.md`, `.ai_hacks/ROUTES_AND_DI.md` Warning: Regenerate generated references after structural changes, but preserve the semantic knowledge in ai_hacks.context.json. |

## Runtime workflows

### Startup and local database initialization

Compose the web host, register the application, initialize the SQLite schema/default data, then map static assets, the automation API, and Blazor components.

`DevLeads.Web.Program` → `DevLeads.Infrastructure.DependencyInjection.AddDevLeads` → `DevLeads.Infrastructure.DependencyInjection.InitializeDevLeadsAsync` → `DevLeads.Infrastructure.Data.DatabaseSeeder.InitializeAsync` → `DevLeads.Web.Api.ApiEndpoints.MapDevLeadsApi`

- DatabaseSeeder performs schema upgrades, default-data reconciliation, and cleanup on every boot; it is not a demo-data seeder.
- Blazor pages generally use IDbContextFactory, while scoped orchestration services share a scoped DevLeadsDbContext.

### Lead discovery and triage

Poll due configured sources, fetch normalized public items, deduplicate and prefilter them, optionally shortlist and batch-triage them, then retain only actionable scored opportunities.

`DevLeads.Infrastructure.Workers.DiscoveryWorker.ExecuteAsync` → `DevLeads.Infrastructure.Workers.DiscoveryWorker.TickAsync` → `DevLeads.Infrastructure.Services.SourceRunner.RunAsync` → `DevLeads.Core.Connectors.ISourceConnector.FetchAsync` → `DevLeads.Core.HeuristicPreFilter.Analyze` → `DevLeads.Infrastructure.Ai.AiTriageRouter.ShortlistAsync` → `DevLeads.Infrastructure.Ai.AiTriageRouter.TriageBatchAsync` → `DevLeads.Infrastructure.Services.LeadIngestionService.IngestAsync` → `DevLeads.Infrastructure.Services.LeadIngestionService.RunTriageScoreAndDraftAsync` → `DevLeads.Core.Scoring.OpportunityScorer.Score` → `DevLeads.Infrastructure.Services.LeadIngestionService.DecideStatusAndDraft`

- SourceConfig.QueryPacksCsv scopes both connector search terms and high-priority prefilter vocabulary.
- SourceConfig.ParametersJson may select a connector whose key differs from the source row's SourceKey.
- Rejected automated candidates keep raw dedup evidence but do not remain visible opportunities.

### AI provider selection

Resolve the provider named by operator or source settings, retry supported calls, and use offline heuristic triage when the selected provider is unknown or unavailable.

`DevLeads.Infrastructure.Ai.AiTriageRouter.Resolve` → `DevLeads.Infrastructure.Ai.AiTriageRouter.TriageAsync` → `DevLeads.Infrastructure.Ai.OpenCodeTriageProvider.TriageAsync` → `DevLeads.Infrastructure.Ai.AnthropicTriageProvider.TriageAsync` → `DevLeads.Infrastructure.Ai.HeuristicTriageProvider.TriageAsync`

- Unavailable OpenCode does not automatically try Anthropic; router fallback is directly to Heuristic.
- Only OpenCode implements batch shortlist and batch triage contracts.
- OpenCode has its own configured-model fallback chain.

### Human-approved outreach

Queue qualified leads, generate grounded replies in OpenCode batches, require approval where configured, then record sending and schedule follow-up.

`DevLeads.Infrastructure.Services.LeadIngestionService.CreateDraft` → `DevLeads.Infrastructure.Services.OutreachService.QueueForGenerationAsync` → `DevLeads.Infrastructure.Services.OutreachService.GenerateQueuedResponsesAsync` → `DevLeads.Infrastructure.Services.OutreachService.ApproveAsync` → `DevLeads.Infrastructure.Services.OutreachService.SendAsync`

- SendAsync records a manual or draft send; this repository has no external message-delivery integration.
- The global kill switch, approval requirement, and suppression list are enforced at send time.
- Queued generation injects OpenCode directly rather than using AiTriageRouter.

### Quotes and delivery tracking

Create a bounded-work quote, track send/payment state, and manage execution notes and checklists from the opportunity detail page.

`DevLeads.Infrastructure.Services.QuoteService.GenerateAsync` → `DevLeads.Infrastructure.Services.QuoteService.SendAsync` → `DevLeads.Infrastructure.Services.QuoteService.MarkPaidAsync` → `DevLeads.Web.Components.Pages.OpportunityDetail`


### Content discovery and drafting

Poll trend-only sources into ranked signals, periodically suggest topics, and generate operator-requested publishable drafts without creating leads.

`DevLeads.Infrastructure.Workers.ContentTrendWorker.ExecuteAsync` → `DevLeads.Infrastructure.Services.TrendScanService.RunDueAsync` → `DevLeads.Infrastructure.Services.TrendScanService.RunSourceAsync` → `DevLeads.Infrastructure.Services.ContentStudioService.GenerateTopicsAsync` → `DevLeads.Infrastructure.Services.ContentStudioService.GenerateDraftAsync`

- TrendSource and TrendSignal are intentionally separate from SourceConfig, RawSourceItem, and Opportunity.
- Content generation injects OpenCode directly rather than using AiTriageRouter.
- Automatic topic suggestions run at most once per day and only with enough fresh evidence; draft generation is operator-initiated.

### LinkedIn profile management

Connect the operator through state-protected member OAuth, generate/edit text posts, publish immediately or on a worker schedule, and turn approved comment activity or pasted private messages into reviewed response drafts.

`DevLeads.Web.Components.Pages.LinkedIn` → `DevLeads.Web.Api.ApiEndpoints.MapDevLeadsApi` → `DevLeads.Infrastructure.Services.LinkedInService.CreateAuthorizationUrlAsync` → `DevLeads.Infrastructure.Services.LinkedInService.CompleteOAuthAsync` → `DevLeads.Infrastructure.Services.LinkedInService.PublishPostAsync` → `DevLeads.Infrastructure.Services.LinkedInService.SyncEngagementAsync` → `DevLeads.Infrastructure.Services.LinkedInService.GenerateEngagementBatchAsync` → `DevLeads.Infrastructure.Services.LinkedInService.PublishEngagementAsync` → `DevLeads.Infrastructure.Workers.DiscoveryWorker.TickAsync`

- Scheduled LinkedIn publishing runs independently of lead discovery but still honors the global kill switch.
- Comment reads require LinkedIn's restricted member-social read permission; self-serve posting does not grant that permission.
- LinkedIn exposes no general member private-inbox API, so private messages are pasted for drafting and sent manually in LinkedIn.

### Recurring maintenance

Hourly cleanup rejects stale or non-hirable leads, marks overdue quotes, and opportunistically generates queued outreach within the AI budget.

`DevLeads.Infrastructure.Workers.DiscoveryWorker.TickAsync` → `DevLeads.Infrastructure.Services.MaintenanceService.RejectNonHirableVendorSupportAsync` → `DevLeads.Infrastructure.Services.MaintenanceService.ArchiveStaleLeadsAsync` → `DevLeads.Infrastructure.Services.MaintenanceService.FlagOverdueQuotesAsync` → `DevLeads.Infrastructure.Services.OutreachService.GenerateQueuedResponsesAsync`


## Critical invariants

- Every visible opportunity must have a canonical original public SourceUrl.
- A pay-intent signal must represent first-person ownership, explicit hire/pay language, or a concrete paid source; topic keywords such as payments or budget alone do not qualify a lead.
- Explicit paid work ranks above networking leads, but a business owner or operator with a concrete hands-on request and implied future commercial potential may remain for human review even without current payment language; generic free-advice posts still do not qualify.
- Candidates that pass safety and actionability gates but reach only 50–99% of their source opportunity threshold are retained as JustMissed for dashboard scoring review; they remain excluded from normal active-lead and automatic-outreach paths.
- Automated discovery retains raw seen-item evidence for dedup even when no opportunity survives.
- One canonical source URL represents at most one opportunity across connectors; near-duplicate checks additionally use source, host, normalized title, and shared clues.
- Source query packs scope both discovery vocabulary and high-priority prefilter terms to the owning campaign.
- Campaign objectives are passed into AI shortlist, triage, and outreach generation so relevance stays campaign-specific.
- Red-flagged, resolved, promotional, reply-feed, autonomous-agent-task, and non-hirable vendor-support posts must not reach actionable outreach.
- Technology stack is informational and only a light ranking preference; never reject or score-cap a lead solely because it lacks an operator skill-profile match. Paid-work quality, professional compensation, remote feasibility, competition, and poster reliability dominate.
- Automated discovery ignores public posts older than 30 days before AI triage and removes untouched expired leads while retaining raw dedup evidence; manual and operator-engaged leads are preserved, and the dashboard excludes expired posts.
- Non-English leads receive a language-friction score penalty before source thresholds; only survivors store/display an English translation, while the original source text remains available for verification.
- Stated compensation overrides category-based fee estimates and is displayed as an offer rather than an estimate.
- The selected unavailable AI provider falls back to Heuristic; the lead pipeline remains operable without external AI.
- Outreach never bypasses the global kill switch, required approval, or enabled suppression list.
- Trend items become TrendSignals only and never enter the lead/opportunity pipeline.
- Operator-engaged and archived opportunities are never automatically purged by source/default cleanup.
- SQLite DateTimeOffset values are persisted as sortable UTC ticks and enums as readable strings.
- Unknown platform metrics are never presented as observed zero values, and opening an unread My Posts message persists it as Read.
- LinkedIn OAuth callbacks validate the stored short-lived state before exchanging a code, and access tokens are never returned by status endpoints or rendered into the page.
- LinkedIn scheduled posts and public comment replies honor the global kill switch; AI-generated engagement responses require explicit operator publication.
- LinkedIn comment monitoring requires an approved restricted read scope, while private inbox messages remain manual because the general member API does not expose them.

## Type hierarchy

- `BackgroundService` → `ContentTrendWorker`, `DiscoveryWorker`
- `ComponentBase` → `ActivityFeed`, `App`, `CampaignSwitcher`, `Campaigns`, `ClientDetail`, `Clients`, `Content`, `Drafts`, `Error`, `Home`, `LinkedIn`, `MyPosts`, `NavMenu`, `NewOpportunity`, `NotFound`, `Opportunities`, `OpportunityDetail`, `PlatformPresencePanel`, `PostPerformanceChart`, `Quotes`, `ReconnectModal`, `Routes`, `Settings`, `SiteRescue`, `SkillProfile`, `Sources`, `Today`, `_Imports`
- `DbContext` → `DevLeadsDbContext`
- `IAiBatchShortlistProvider` → `CodexCliProvider`, `OpenCodeTriageProvider`
- `IAiBatchTriageProvider` → `CodexCliProvider`, `OpenCodeTriageProvider`
- `IAiTriageProvider` → `AnthropicTriageProvider`, `CodexCliProvider`, `HeuristicTriageProvider`, `OpenCodeTriageProvider`
- `IQueryPackProvider` → `DbQueryPackProvider`
- `ISourceConnector` → `GitHubSearchConnector`, `HackerNewsConnector`, `OpireConnector`, `RedditConnector`, `RemotiveConnector`, `RssConnector`, `StackExchangeConnector`
