# Routes, Dependency Injection, and Persistence

## Blazor page routes

| Route | Component | Purpose | Injects |
| --- | --- | --- | --- |
| / | Home | Campaign-scoped dashboard with lead KPIs, activity, and top opportunities. | IDbContextFactory<DevLeadsDbContext> |
| /campaigns | Campaigns | Campaign objectives and source/lead ownership management. | IDbContextFactory<DevLeadsDbContext> |
| /content | Content | Trend signals, suggested topics, and publishable draft management. | IDbContextFactory<DevLeadsDbContext>, IServiceScopeFactory, IJSRuntime |
| /drafts | Drafts | Outreach generation and human approval queues. | IDbContextFactory<DevLeadsDbContext>, IServiceScopeFactory |
| /Error | Error | Unhandled-error page. |  |
| /not-found | NotFound | Missing-route page. |  |
| /opportunities | Opportunities | Searchable and filterable lead-review queue. | IDbContextFactory<DevLeadsDbContext>, IServiceScopeFactory |
| /opportunities/new | NewOpportunity | Manual lead entry through the normal triage pipeline. | IServiceScopeFactory, IDbContextFactory<DevLeadsDbContext>, NavigationManager |
| /opportunities/{Id:long} | OpportunityDetail | Lead detail, triage, scoring, outreach, quotes, work tracking, and audit history. | IDbContextFactory<DevLeadsDbContext>, IServiceScopeFactory, IJSRuntime |
| /quotes | Quotes | Quote and payment-state management. | IDbContextFactory<DevLeadsDbContext>, IServiceScopeFactory |
| /settings | Settings | Operator, AI, safety, discovery, and restart settings. | IDbContextFactory<DevLeadsDbContext>, IServiceScopeFactory, AiTriageRouter, DevLeads.Web.AppRestartService |
| /skills | SkillProfile | Operator skill-profile management. | IDbContextFactory<DevLeadsDbContext> |
| /sources | Sources | Source configuration, health checks, and manual discovery runs. | IDbContextFactory<DevLeadsDbContext>, IServiceScopeFactory |

## HTTP endpoints

| Verb | Route | Owner | Purpose | Source |
| --- | --- | --- | --- | --- |
| GET | /api/campaigns | MapDevLeadsApi | Reads campaigns. | src/DevLeads.Web/Api/ApiEndpoints.cs:17 |
| GET | /api/content/drafts | MapDevLeadsApi | Reads drafts. | src/DevLeads.Web/Api/ApiEndpoints.cs:148 |
| POST | /api/content/scan | MapDevLeadsApi | Runs the scan action. | src/DevLeads.Web/Api/ApiEndpoints.cs:129 |
| GET | /api/content/signals | MapDevLeadsApi | Reads signals. | src/DevLeads.Web/Api/ApiEndpoints.cs:131 |
| GET | /api/content/topics | MapDevLeadsApi | Reads topics. | src/DevLeads.Web/Api/ApiEndpoints.cs:139 |
| POST | /api/content/topics/generate | MapDevLeadsApi | Runs the generate action. | src/DevLeads.Web/Api/ApiEndpoints.cs:134 |
| POST | /api/content/topics/{id:long}/drafts | MapDevLeadsApi | Runs the drafts action. | src/DevLeads.Web/Api/ApiEndpoints.cs:141 |
| GET | /api/opportunities | MapDevLeadsApi | Reads opportunities. | src/DevLeads.Web/Api/ApiEndpoints.cs:21 |
| POST | /api/opportunities/manual | MapDevLeadsApi | Runs the manual action. | src/DevLeads.Web/Api/ApiEndpoints.cs:39 |
| GET | /api/opportunities/{id:long} | MapDevLeadsApi | Reads id. | src/DevLeads.Web/Api/ApiEndpoints.cs:31 |
| POST | /api/opportunities/{id:long}/approve | MapStatusAction | Runs the approve action. | src/DevLeads.Web/Api/ApiEndpoints.cs:52 |
| POST | /api/opportunities/{id:long}/archive | MapStatusAction | Runs the archive action. | src/DevLeads.Web/Api/ApiEndpoints.cs:54 |
| POST | /api/opportunities/{id:long}/draft-response | MapDevLeadsApi | Runs the draft response action. | src/DevLeads.Web/Api/ApiEndpoints.cs:74 |
| POST | /api/opportunities/{id:long}/generate-quote | MapDevLeadsApi | Runs the generate quote action. | src/DevLeads.Web/Api/ApiEndpoints.cs:92 |
| POST | /api/opportunities/{id:long}/mark-contacted | MapStatusAction | Runs the mark contacted action. | src/DevLeads.Web/Api/ApiEndpoints.cs:56 |
| POST | /api/opportunities/{id:long}/mark-lost | MapStatusAction | Runs the mark lost action. | src/DevLeads.Web/Api/ApiEndpoints.cs:58 |
| POST | /api/opportunities/{id:long}/mark-won | MapStatusAction | Runs the mark won action. | src/DevLeads.Web/Api/ApiEndpoints.cs:57 |
| POST | /api/opportunities/{id:long}/queue-response | MapDevLeadsApi | Runs the queue response action. | src/DevLeads.Web/Api/ApiEndpoints.cs:83 |
| POST | /api/opportunities/{id:long}/reject | MapStatusAction | Runs the reject action. | src/DevLeads.Web/Api/ApiEndpoints.cs:53 |
| POST | /api/opportunities/{id:long}/rerun-triage | MapDevLeadsApi | Runs the rerun triage action. | src/DevLeads.Web/Api/ApiEndpoints.cs:60 |
| POST | /api/opportunities/{id:long}/run-ai-triage | MapDevLeadsApi | Runs the run ai triage action. | src/DevLeads.Web/Api/ApiEndpoints.cs:65 |
| GET | /api/opportunities/{id:long}/triage-runs | MapDevLeadsApi | Reads triage runs. | src/DevLeads.Web/Api/ApiEndpoints.cs:70 |
| POST | /api/opportunities/{id:long}/watch | MapStatusAction | Runs the watch action. | src/DevLeads.Web/Api/ApiEndpoints.cs:55 |
| POST | /api/outreach/generate-queued | MapDevLeadsApi | Runs the generate queued action. | src/DevLeads.Web/Api/ApiEndpoints.cs:85 |
| POST | /api/outreach/{id:long}/approve | MapDevLeadsApi | Runs the approve action. | src/DevLeads.Web/Api/ApiEndpoints.cs:76 |
| POST | /api/outreach/{id:long}/cancel | MapDevLeadsApi | Runs the cancel action. | src/DevLeads.Web/Api/ApiEndpoints.cs:82 |
| POST | /api/outreach/{id:long}/send | MapDevLeadsApi | Runs the send action. | src/DevLeads.Web/Api/ApiEndpoints.cs:77 |
| POST | /api/quotes/{id:long}/mark-overdue | MapDevLeadsApi | Runs the mark overdue action. | src/DevLeads.Web/Api/ApiEndpoints.cs:96 |
| POST | /api/quotes/{id:long}/mark-paid | MapDevLeadsApi | Runs the mark paid action. | src/DevLeads.Web/Api/ApiEndpoints.cs:95 |
| POST | /api/quotes/{id:long}/send | MapDevLeadsApi | Runs the send action. | src/DevLeads.Web/Api/ApiEndpoints.cs:94 |
| GET | /api/sources | MapDevLeadsApi | Reads sources. | src/DevLeads.Web/Api/ApiEndpoints.cs:99 |
| POST | /api/sources/run-all | MapDevLeadsApi | Runs the run all action. | src/DevLeads.Web/Api/ApiEndpoints.cs:100 |
| POST | /api/sources/{key}/run-now | MapDevLeadsApi | Runs the run now action. | src/DevLeads.Web/Api/ApiEndpoints.cs:120 |
| POST | /api/sources/{key}/test | MapDevLeadsApi | Runs the test action. | src/DevLeads.Web/Api/ApiEndpoints.cs:118 |
| POST | /api/system/restart | MapDevLeadsApi | Runs the restart action. | src/DevLeads.Web/Api/ApiEndpoints.cs:152 |
| GET | /favicon.ico | startup | Reads favicon.ico. | src/DevLeads.Web/Program.cs:43 |

## Dependency injection

| Registration | Service | Implementation | Source |
| --- | --- | --- | --- |
| DbContextFactory | IDbContextFactory<DevLeadsDbContext> | DevLeadsDbContext | src/DevLeads.Infrastructure/DependencyInjection.cs:24 |
| Scoped | DevLeadsDbContext | IDbContextFactory<DevLeadsDbContext | src/DevLeads.Infrastructure/DependencyInjection.cs:25 |
| HttpClient | named HttpClient | HttpClient | src/DevLeads.Infrastructure/DependencyInjection.cs:29 |
| Transient | ISourceConnector | RssConnector | src/DevLeads.Infrastructure/DependencyInjection.cs:41 |
| Transient | ISourceConnector | HackerNewsConnector | src/DevLeads.Infrastructure/DependencyInjection.cs:42 |
| Transient | ISourceConnector | StackExchangeConnector | src/DevLeads.Infrastructure/DependencyInjection.cs:43 |
| Transient | ISourceConnector | RemotiveConnector | src/DevLeads.Infrastructure/DependencyInjection.cs:44 |
| Transient | ISourceConnector | RedditConnector | src/DevLeads.Infrastructure/DependencyInjection.cs:45 |
| Transient | ISourceConnector | OpireConnector | src/DevLeads.Infrastructure/DependencyInjection.cs:46 |
| Transient | ISourceConnector | GitHubSearchConnector | src/DevLeads.Infrastructure/DependencyInjection.cs:47 |
| Scoped | IQueryPackProvider | DbQueryPackProvider | src/DevLeads.Infrastructure/DependencyInjection.cs:50 |
| Scoped | HeuristicPreFilter | HeuristicPreFilter | src/DevLeads.Infrastructure/DependencyInjection.cs:51 |
| Singleton | HeuristicTriageProvider | HeuristicTriageProvider | src/DevLeads.Infrastructure/DependencyInjection.cs:56 |
| Singleton | AnthropicTriageProvider | AnthropicTriageProvider | src/DevLeads.Infrastructure/DependencyInjection.cs:57 |
| Singleton | OpenCodeTriageProvider | OpenCodeTriageProvider | src/DevLeads.Infrastructure/DependencyInjection.cs:58 |
| Singleton | IAiTriageProvider | OpenCodeTriageProvider | src/DevLeads.Infrastructure/DependencyInjection.cs:59 |
| Singleton | IAiTriageProvider | AnthropicTriageProvider | src/DevLeads.Infrastructure/DependencyInjection.cs:60 |
| Singleton | IAiTriageProvider | HeuristicTriageProvider | src/DevLeads.Infrastructure/DependencyInjection.cs:61 |
| Singleton | AiTriageRouter | AiTriageRouter | src/DevLeads.Infrastructure/DependencyInjection.cs:62 |
| Singleton | DiscoveryActivityTracker | DiscoveryActivityTracker | src/DevLeads.Infrastructure/DependencyInjection.cs:65 |
| Scoped | AuditService | AuditService | src/DevLeads.Infrastructure/DependencyInjection.cs:68 |
| Scoped | LeadIngestionService | LeadIngestionService | src/DevLeads.Infrastructure/DependencyInjection.cs:69 |
| Scoped | OutreachService | OutreachService | src/DevLeads.Infrastructure/DependencyInjection.cs:70 |
| Scoped | QuoteService | QuoteService | src/DevLeads.Infrastructure/DependencyInjection.cs:71 |
| Scoped | SourceRunner | SourceRunner | src/DevLeads.Infrastructure/DependencyInjection.cs:72 |
| Scoped | MaintenanceService | MaintenanceService | src/DevLeads.Infrastructure/DependencyInjection.cs:73 |
| Scoped | TrendScanService | TrendScanService | src/DevLeads.Infrastructure/DependencyInjection.cs:74 |
| Scoped | ContentStudioService | ContentStudioService | src/DevLeads.Infrastructure/DependencyInjection.cs:75 |
| HostedService | IHostedService | DiscoveryWorker | src/DevLeads.Infrastructure/DependencyInjection.cs:78 |
| HostedService | IHostedService | ContentTrendWorker | src/DevLeads.Infrastructure/DependencyInjection.cs:79 |
| Singleton | DevLeads.Web.AppRestartService | DevLeads.Web.AppRestartService | src/DevLeads.Web/Program.cs:25 |

## EF Core DbSets

| Context | Entity | Property | Source |
| --- | --- | --- | --- |
| DevLeadsDbContext | AiTriageRun | AiTriageRuns | src/DevLeads.Infrastructure/Data/DevLeadsDbContext.cs:15 |
| DevLeadsDbContext | AuditEvent | AuditEvents | src/DevLeads.Infrastructure/Data/DevLeadsDbContext.cs:20 |
| DevLeadsDbContext | Campaign | Campaigns | src/DevLeads.Infrastructure/Data/DevLeadsDbContext.cs:25 |
| DevLeadsDbContext | ContentDraft | ContentDrafts | src/DevLeads.Infrastructure/Data/DevLeadsDbContext.cs:29 |
| DevLeadsDbContext | ContentTopic | ContentTopics | src/DevLeads.Infrastructure/Data/DevLeadsDbContext.cs:28 |
| DevLeadsDbContext | OperatorSettings | OperatorSettings | src/DevLeads.Infrastructure/Data/DevLeadsDbContext.cs:23 |
| DevLeadsDbContext | Opportunity | Opportunities | src/DevLeads.Infrastructure/Data/DevLeadsDbContext.cs:13 |
| DevLeadsDbContext | OutreachAttempt | OutreachAttempts | src/DevLeads.Infrastructure/Data/DevLeadsDbContext.cs:16 |
| DevLeadsDbContext | QueryPack | QueryPacks | src/DevLeads.Infrastructure/Data/DevLeadsDbContext.cs:22 |
| DevLeadsDbContext | Quote | Quotes | src/DevLeads.Infrastructure/Data/DevLeadsDbContext.cs:17 |
| DevLeadsDbContext | RawSourceItem | RawSourceItems | src/DevLeads.Infrastructure/Data/DevLeadsDbContext.cs:14 |
| DevLeadsDbContext | Skill | Skills | src/DevLeads.Infrastructure/Data/DevLeadsDbContext.cs:24 |
| DevLeadsDbContext | SourceConfig | SourceConfigs | src/DevLeads.Infrastructure/Data/DevLeadsDbContext.cs:21 |
| DevLeadsDbContext | SuppressionEntry | SuppressionEntries | src/DevLeads.Infrastructure/Data/DevLeadsDbContext.cs:19 |
| DevLeadsDbContext | TrendSignal | TrendSignals | src/DevLeads.Infrastructure/Data/DevLeadsDbContext.cs:27 |
| DevLeadsDbContext | TrendSource | TrendSources | src/DevLeads.Infrastructure/Data/DevLeadsDbContext.cs:26 |
| DevLeadsDbContext | WorkSession | WorkSessions | src/DevLeads.Infrastructure/Data/DevLeadsDbContext.cs:18 |

## Inferred entity relationships

| Source | Cardinality | Target | Navigation |
| --- | --- | --- | --- |
| AiTriageRun | many-to-one | Opportunity | Opportunity |
| ContentDraft | many-to-one | ContentTopic | Topic |
| ContentTopic | one-to-many | ContentDraft | Drafts |
| Opportunity | one-to-many | AiTriageRun | TriageRuns |
| Opportunity | one-to-many | OutreachAttempt | OutreachAttempts |
| Opportunity | one-to-many | Quote | Quotes |
| Opportunity | one-to-many | WorkSession | WorkSessions |
| OutreachAttempt | many-to-one | Opportunity | Opportunity |
| Quote | many-to-one | Opportunity | Opportunity |
| RawSourceItem | many-to-one | Opportunity | Opportunity |
| WorkSession | many-to-one | Opportunity | Opportunity |
