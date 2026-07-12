# DevLeads AI Project Context

> Generated; do not hand-edit. Schema 2, source digest `fb2987f1ada8fd3c0e674d1e2b035cd8817f7b3b470fc784bd71b98326def38e`.
> Regenerate with `python3 ai_hacks.py`; verify freshness with `python3 ai_hacks.py --check`.

## How to use this map

1. Use **Change routing** to choose the owning symbol and likely downstream files.
2. Use the hierarchy, routes, DI, and catalog to narrow the task before opening implementation bodies.
3. Read only the selected implementation and direct collaborators. Deeper generated references live beside this file.

The navigation rules, change-routing warnings, and critical invariants in this document are project instructions. Follow them unless a higher-priority user or system instruction explicitly overrides them. Repository bootstrap files for Codex, Claude, Gemini, and GitHub Copilot point agents here automatically.

Descriptions marked _(inferred)_ come from deterministic name/signature rules; all others come from source XML docs or the checked-in architecture overlay. No implementation code is copied here.

---

# Architecture and Navigation Guide

A solo-operator Blazor application that discovers public paid software-work leads, triages and scores them, manages human-approved outreach and quotes, and turns separate trend signals into content drafts.

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

### Recurring maintenance

Hourly cleanup rejects stale or non-hirable leads, marks overdue quotes, and opportunistically generates queued outreach within the AI budget.

`DevLeads.Infrastructure.Workers.DiscoveryWorker.TickAsync` → `DevLeads.Infrastructure.Services.MaintenanceService.RejectNonHirableVendorSupportAsync` → `DevLeads.Infrastructure.Services.MaintenanceService.ArchiveStaleLeadsAsync` → `DevLeads.Infrastructure.Services.MaintenanceService.FlagOverdueQuotesAsync` → `DevLeads.Infrastructure.Services.OutreachService.GenerateQueuedResponsesAsync`


## Critical invariants

- Every visible opportunity must have a canonical original public SourceUrl.
- A pay-intent signal must represent first-person ownership, explicit hire/pay language, or a concrete paid source; topic keywords such as payments or budget alone do not qualify a lead.
- Automated discovery retains raw seen-item evidence for dedup even when no opportunity survives.
- One canonical source URL represents at most one opportunity across connectors; near-duplicate checks additionally use source, host, normalized title, and shared clues.
- Source query packs scope both discovery vocabulary and high-priority prefilter terms to the owning campaign.
- Campaign objectives are passed into AI shortlist, triage, and outreach generation so relevance stays campaign-specific.
- Red-flagged, resolved, promotional, reply-feed, autonomous-agent-task, and non-hirable vendor-support posts must not reach actionable outreach.
- Foreign-stack work without an operator stack-identity match is rejected or score-capped; generic capabilities such as API work are not stack identity.
- Stated compensation overrides category-based fee estimates and is displayed as an offer rather than an estimate.
- The selected unavailable AI provider falls back to Heuristic; the lead pipeline remains operable without external AI.
- Outreach never bypasses the global kill switch, required approval, or enabled suppression list.
- Trend items become TrendSignals only and never enter the lead/opportunity pipeline.
- Operator-engaged and archived opportunities are never automatically purged by source/default cleanup.
- SQLite DateTimeOffset values are persisted as sortable UTC ticks and enums as readable strings.

## Type hierarchy

- `BackgroundService` → `ContentTrendWorker`, `DiscoveryWorker`
- `ComponentBase` → `ActivityFeed`, `App`, `CampaignSwitcher`, `Campaigns`, `Content`, `Drafts`, `Error`, `Home`, `NavMenu`, `NewOpportunity`, `NotFound`, `Opportunities`, `OpportunityDetail`, `Quotes`, `ReconnectModal`, `Routes`, `Settings`, `SkillProfile`, `Sources`, `_Imports`
- `DbContext` → `DevLeadsDbContext`
- `IAiTriageProvider` → `AnthropicTriageProvider`, `HeuristicTriageProvider`, `OpenCodeTriageProvider`
- `IQueryPackProvider` → `DbQueryPackProvider`
- `ISourceConnector` → `GitHubSearchConnector`, `HackerNewsConnector`, `OpireConnector`, `RedditConnector`, `RemotiveConnector`, `RssConnector`, `StackExchangeConnector`


---

# Runtime Surface Summary

## Blazor pages

- `/` → `Home` — Campaign-scoped dashboard with lead KPIs, activity, and top opportunities.
- `/campaigns` → `Campaigns` — Campaign objectives and source/lead ownership management.
- `/content` → `Content` — Trend signals, suggested topics, and publishable draft management.
- `/drafts` → `Drafts` — Outreach generation and human approval queues.
- `/Error` → `Error` — Unhandled-error page.
- `/not-found` → `NotFound` — Missing-route page.
- `/opportunities` → `Opportunities` — Searchable and filterable lead-review queue.
- `/opportunities/new` → `NewOpportunity` — Manual lead entry through the normal triage pipeline.
- `/opportunities/{Id:long}` → `OpportunityDetail` — Lead detail, triage, scoring, outreach, quotes, work tracking, and audit history.
- `/quotes` → `Quotes` — Quote and payment-state management.
- `/settings` → `Settings` — Operator, AI, safety, discovery, and restart settings.
- `/skills` → `SkillProfile` — Operator skill-profile management.
- `/sources` → `Sources` — Source configuration, health checks, and manual discovery runs.

## HTTP, DI, and data

- HTTP endpoint groups: `/api/campaigns` (1), `/api/content` (6), `/api/opportunities` (16), `/api/outreach` (4), `/api/quotes` (3), `/api/sources` (4), `/api/system` (1), `/favicon.ico` (1).
- Hosted workers: `DiscoveryWorker`, `ContentTrendWorker`.
- EF DbSets: `AiTriageRuns`, `AuditEvents`, `Campaigns`, `ContentDrafts`, `ContentTopics`, `OperatorSettings`, `Opportunities`, `OutreachAttempts`, `QueryPacks`, `Quotes`, `RawSourceItems`, `Skills`, `SourceConfigs`, `SuppressionEntries`, `TrendSignals`, `TrendSources`, `WorkSessions`.
- Complete endpoint, registration, and relationship tables: `ROUTES_AND_DI.md`.


---

# Complete Type and Method Index

Every source-authored type and callable name is present. Full signatures and data members are in `PUBLIC_API.md`.

## DevLeads.Core

- **`AiTriagePrompts`** — The unified system prompt, user template, and strict JSON schema for single-pass triage. (`src/DevLeads.Core/Ai/AiTriagePrompts.cs:4`)
  - public `BuildUserPrompt` — Fills the user-prompt template with post + pre-filter context.
  - public `BuildBatchUserPrompt` — Fills the user-prompt template for a batched call: several posts, one response object per…
- **`ContentPrompts`** — Prompts for the content studio: topic suggestion and long-form draft generation. (`src/DevLeads.Core/Ai/ContentPrompts.cs:8`)
  - public `BuildTopicPrompt` — Asks for publishable topics distilled from trend signals. Output is strict JSON…
  - public `BuildDraftPrompt` — Asks for a complete piece in the requested format. Output is plain markdown whose first…
  - private `FormatSpec` — Transforms or resolves spec. _(inferred)_
  - public `ParseEvidence` — Transforms or resolves evidence. _(inferred)_
  - private `Compact` — Transforms or resolves compact. _(inferred)_
- **`AiTriageRequest`** — Input to the single-pass triage call. (`src/DevLeads.Core/Ai/IAiTriageProvider.cs:6`)
- **`AiTriageResponse`** — Outcome of a triage call, including provider metadata for the audit trail. (`src/DevLeads.Core/Ai/IAiTriageProvider.cs:23`)
- **`AiShortlistItem`** — Compact candidate shown to an AI provider before spending a full triage call. (`src/DevLeads.Core/Ai/IAiTriageProvider.cs:36`)
- **`AiShortlistDecision`** — Represents ai shortlist decision. _(inferred)_ (`src/DevLeads.Core/Ai/IAiTriageProvider.cs:47`)
- **`AiShortlistResponse`** — Represents ai shortlist response. _(inferred)_ (`src/DevLeads.Core/Ai/IAiTriageProvider.cs:54`)
- **`IAiBatchShortlistProvider`** — Defines the ai batch shortlist provider contract. _(inferred)_ (`src/DevLeads.Core/Ai/IAiTriageProvider.cs:66`)
  - public `ShortlistAsync` — Handles shortlist. _(inferred)_
- **`AiBatchTriageItem`** — One item inside a batched triage call, keyed so results map back. (`src/DevLeads.Core/Ai/IAiTriageProvider.cs:77`)
- **`AiBatchTriageResponse`** — Outcome of one batched triage call: per-item results keyed by item id. (`src/DevLeads.Core/Ai/IAiTriageProvider.cs:84`)
- **`IAiBatchTriageProvider`** — Providers that can triage several posts in one model call. Batching is the main AI cost lever: N shortlisted items become ceil(N/chunk) calls… (`src/DevLeads.Core/Ai/IAiTriageProvider.cs:100`)
  - public `TriageBatchAsync` — Coordinates batch. _(inferred)_
- **`IAiTriageProvider`** — Abstraction over the AI triage backend. Providers are registered by name and selected at runtime through operator settings, so decision-making AI is… (`src/DevLeads.Core/Ai/IAiTriageProvider.cs:113`)
  - public `IsAvailable` — Whether the provider can currently make calls (CLI present, key set, …).
  - public `AvailabilityMessage` — Human-readable explanation when IsAvailable is false.
  - public `TriageAsync` — Coordinates triage. _(inferred)_
- **`OutreachGenerationItem`** — One queued lead inside a batched response-generation call. (`src/DevLeads.Core/Ai/OutreachPrompts.cs:7`)
- **`OutreachPrompts`** — Prompt for batched outreach-response generation: every queued lead in one model call, each reply grounded strictly in that lead's original post. (`src/DevLeads.Core/Ai/OutreachPrompts.cs:22`)
  - public `BuildBatchResponsePrompt` — Creates batch response prompt. _(inferred)_
  - private `Compact` — Transforms or resolves compact. _(inferred)_
- **`AiTriageResult`** — The strict structured object returned by the single-pass AI triage call. Satisfies all former pipeline stages (relevance, emergency, category… (`src/DevLeads.Core/AiTriageResult.cs:10`)
- **`SourceConnectorConfig`** — Runtime configuration passed to a connector for a single fetch. (`src/DevLeads.Core/Connectors/ISourceConnector.cs:6`)
- **`ConnectorHealth`** — Reported health of a connector after a run or health check. (`src/DevLeads.Core/Connectors/ISourceConnector.cs:19`)
- **`ISourceConnector`** — A read-only ingestion source. Fetches recent public items, respects rate limits, and never sends messages. (`src/DevLeads.Core/Connectors/ISourceConnector.cs:31`)
  - public `FetchAsync` — Loads or resolves fetch. _(inferred)_
  - public `CheckHealthAsync` — Checks health. _(inferred)_
- **`AiTriageRun`** — An auditable record of a single-pass structured AI triage call. (`src/DevLeads.Core/Entities/AiTriageRun.cs:4`)
- **`AuditEvent`** — An immutable audit-trail entry for anything the system generates, sends, or changes. (`src/DevLeads.Core/Entities/AuditEvent.cs:4`)
- **`Campaign`** — A lead-generation campaign: a named objective (e.g. emergency rescue work,.NET legacy modernization consulting) that owns a set of sources and the… (`src/DevLeads.Core/Entities/Campaign.cs:8`)
- **`ContentDraft`** — A generated piece of publishable content (blog post, article, white paper, research paper, or LinkedIn post) for the operator to edit and post on… (`src/DevLeads.Core/Entities/ContentDraft.cs:7`)
- **`ContentTopic`** — An AI-suggested publishing topic distilled from trend signals: what to write about, the specific angle, and why an audience would care right now. (`src/DevLeads.Core/Entities/ContentTopic.cs:7`)
- **`OperatorSettings`** — Single-row settings for the solo operator: profile, AI, outreach, and safety controls. (`src/DevLeads.Core/Entities/OperatorSettings.cs:4`)
- **`Opportunity`** — A triaged, scored emergency-repair lead. The central aggregate the whole app revolves around. (`src/DevLeads.Core/Entities/Opportunity.cs:6`)
- **`OutreachAttempt`** — A drafted, approved, or sent outreach message tied to an opportunity. (`src/DevLeads.Core/Entities/OutreachAttempt.cs:4`)
- **`QueryPack`** — A named set of search/keyword terms used by connectors and the heuristic pre-filter. (`src/DevLeads.Core/Entities/QueryPack.cs:4`)
- **`Quote`** — A flat-fee emergency-repair quote and its payment lifecycle. (`src/DevLeads.Core/Entities/Quote.cs:4`)
- **`RawSourceItem`** — A normalized public item fetched from a source connector, stored before/after triage. Also serves as the connector output DTO. (`src/DevLeads.Core/Entities/RawSourceItem.cs:7`)
- **`Skill`** — One operator skill (language, framework, application, capability…). Used to score how well a lead fits the operator, to filter bounty/issue… (`src/DevLeads.Core/Entities/Skill.cs:8`)
- **`SourceConfig`** — Per-connector configuration and health, editable from the Sources page. (`src/DevLeads.Core/Entities/SourceConfig.cs:4`)
- **`SuppressionEntry`** — A contact that must never be messaged (opt-out, complaint, or manual block). (`src/DevLeads.Core/Entities/SuppressionEntry.cs:4`)
- **`TrendSignal`** — One piece of evidence that something is trending: a hot post, a release note, an announcement. (`src/DevLeads.Core/Entities/TrendSignal.cs:7`)
- **`TrendSource`** — A feed/community polled for *content* signals (trending topics, releases, updates) rather than leads. (`src/DevLeads.Core/Entities/TrendSource.cs:8`)
- **`WorkSession`** — Tracks execution once a lead becomes real work: checklist, notes, fix summary. (`src/DevLeads.Core/Entities/WorkSession.cs:4`)
- **`OpportunityStatus`** — Workflow states an opportunity moves through from discovery to payment. (`src/DevLeads.Core/Enums.cs:4`)
- **`Priority`** — Priority band derived from the weighted opportunity score. (`src/DevLeads.Core/Enums.cs:34`)
- **`AiJobStatus`** — Lifecycle of the single-pass AI triage job for an item. (`src/DevLeads.Core/Enums.cs:44`)
- **`OutreachRecommendation`** — What the system recommends doing with an opportunity. (`src/DevLeads.Core/Enums.cs:56`)
- **`OutreachMode`** — Outreach delivery mode for a given source/template/contact combination. (`src/DevLeads.Core/Enums.cs:66`)
- **`OutreachStatus`** — Lifecycle of a single outreach attempt. (`src/DevLeads.Core/Enums.cs:76`)
- **`OutreachChannel`** — Channel an outreach attempt is delivered over. (`src/DevLeads.Core/Enums.cs:90`)
- **`QuoteStatus`** — Payment lifecycle for a quote. (`src/DevLeads.Core/Enums.cs:100`)
- **`WorkSessionStatus`** — Execution state for a hands-on work session. (`src/DevLeads.Core/Enums.cs:115`)
- **`ContentTopicStatus`** — Lifecycle of an AI-suggested publishing topic. (`src/DevLeads.Core/Enums.cs:126`)
- **`ContentDraftStatus`** — Lifecycle of a generated content draft. (`src/DevLeads.Core/Enums.cs:134`)
- **`ContentFormat`** — Publishable formats the content studio can generate. (`src/DevLeads.Core/Enums.cs:143`)
- **`SuppressionContactType`** — How a contact was added to the suppression list. (`src/DevLeads.Core/Enums.cs:153`)
- **`HeuristicPreFilter`** — Zero-cost keyword/rule filter deciding whether a raw item is worth an LLM call. Protects the AI budget, cuts latency, and rejects obvious noise… (`src/DevLeads.Core/HeuristicPreFilter.cs:11`)
  - public `HasPayLanguage` — True when the text contains explicit hire/pay language or a money amount, un-negated.
  - public `Analyze` — Analyzes an item. When packNames is given, high-priority term matching is scoped to those…
  - private `MatchSignals` — Handles match signals. _(inferred)_
- **`LeadQualityRules`** — Shared lead-quality rules used before a post reaches the review queue. (`src/DevLeads.Core/LeadQualityRules.cs:6`)
  - public `IsPromotionalAnnouncement` — True for product-launch/showcase posts: launch language plus the poster's own pricing…
  - public `IsReplyFeedItem` — True for feed items that are replies into an existing thread (WordPress.org reply feeds…
  - public `IsAlreadyClaimed` — True when the post shows someone else already owns the work: the issue is assigned, or the…
  - public `CompetingResponseCount` — How many other people are already engaging with the post: GitHub comments, or Discourse…
  - public `IsAiAgentTaskPost` — Checks ai agent task post. _(inferred)_
  - public `HasThirdPartyPayOffer` — Checks third party pay offer. _(inferred)_
  - public `IsVendorControlledSupportRequest` — Checks vendor controlled support request. _(inferred)_
  - public `IsNonHirableVendorSupportRequest` — Checks non hirable vendor support request. _(inferred)_
  - public `IsResolvedOrClosedRequest` — Checks resolved or closed request. _(inferred)_
  - public `IsConcretePaidSource` — Checks concrete paid source. _(inferred)_
  - public `IsDashboardWorthyLead` — Checks dashboard worthy lead. _(inferred)_
  - public `NormalizeDuplicateTitle` — Transforms or resolves duplicate title. _(inferred)_
  - public `HostFromUrl` — Handles host from url. _(inferred)_
  - public `SharesDuplicateClue` — Handles shares duplicate clue. _(inferred)_
  - private `ExtractDuplicateClues` — Transforms or resolves duplicate clues. _(inferred)_
  - private `NormalizeHost` — Transforms or resolves host. _(inferred)_
- **`OfferedCompensation`** — Extracts a compensation amount the poster explicitly stated ("Reward: $15", "[Bounty $250]", "budget of $500–$800"). (`src/DevLeads.Core/OfferedCompensation.cs:11`)
  - public `Extract` — Returns the stated amount range, or null when no explicit offer exists.
- **`PreFilterResult`** — Result of the zero-cost heuristic pre-filter that gates AI analysis. (`src/DevLeads.Core/PreFilterResult.cs:4`)
- **`QueryPackSeed`** — Seed definition for a query pack. (`src/DevLeads.Core/QueryPacks/DefaultQueryPacks.cs:4`)
- **`DefaultQueryPacks`** — The built-in query packs from the design document, used to seed the database. (`src/DevLeads.Core/QueryPacks/DefaultQueryPacks.cs:7`)
- **`IQueryPackProvider`** — Supplies keyword sets (query packs) to connectors and the heuristic pre-filter. (`src/DevLeads.Core/QueryPacks/IQueryPackProvider.cs:4`)
  - public `GetTerms` — Returns the terms for a named pack (empty if unknown).
  - public `GetHighPriorityTerms` — All high-priority emergency terms across packs.
  - public `GetHighPriorityTerms` — High-priority terms restricted to the named packs, so a source (and its campaign) is…
  - public `GetNegativeTerms` — All negative / exclusion terms.
- **`RedFlagResult`** — Outcome of a red-flag scan. (`src/DevLeads.Core/RedFlagDetector.cs:4`)
- **`RedFlagDetector`** — Flags posts that request unauthorized access, credential theft, malware, fraud, or otherwise carry ownership/authorization risk. (`src/DevLeads.Core/RedFlagDetector.cs:13`)
  - public `Scan` — Coordinates scan. _(inferred)_
- **`ScoreBreakdown`** — The blended score plus its weighted components. (`src/DevLeads.Core/Scoring/OpportunityScorer.cs:6`)
- **`ScoringInput`** — Inputs the scorer needs, decoupled from persistence. (`src/DevLeads.Core/Scoring/OpportunityScorer.cs:19`)
- **`OpportunityScorer`** — Blends heuristic, AI, source-reputation, recency, stack-fit, business-value, reachability and trust signals into a single weighted opportunity score. (`src/DevLeads.Core/Scoring/OpportunityScorer.cs:54`)
  - public `Score` — Handles score. _(inferred)_
  - private `PayHits` — Count of explicit "pay:" hits the pre-filter tagged (hire language, budgets, money amounts).
  - private `HasPaySignal` — Any evidence the poster would actually pay: pay-intent source, AI judgment, or pay language.
  - public `ToPriority` — Handles to priority. _(inferred)_
  - private `Urgency` — Handles urgency. _(inferred)_
  - private `CategorySeverityBonus` — Handles category severity bonus. _(inferred)_
  - private `StackFit` — Handles stack fit. _(inferred)_
  - private `BusinessValue` — Handles business value. _(inferred)_
  - private `Reachability` — Handles reachability. _(inferred)_
  - private `Competition` — Handles competition. _(inferred)_
  - private `SourceBaseCompetition` — Handles source base competition. _(inferred)_
  - private `Trust` — Handles trust. _(inferred)_
  - private `SourceReputation` — Handles source reputation. _(inferred)_
  - private `IsPayIntentSource` — Checks pay intent source. _(inferred)_
- **`DefaultSkills`** — The operator's seeded skill profile (from the operator's own skillset document). Only seeds when the Skills table is empty — the Skills page is the… (`src/DevLeads.Core/Skills/DefaultSkills.cs:10`)
  - private `S` — Handles s. _(inferred)_
- **`SkillMatch`** — A skill that matched a piece of lead text, with its profile weight and category. (`src/DevLeads.Core/Skills/SkillMatcher.cs:7`)
- **`SkillMatcher`** — Matches lead text against the operator's skill profile and scores the fit. (`src/DevLeads.Core/Skills/SkillMatcher.cs:10`)
  - public `Match` — All enabled skills whose name or any alias appears in the text (case-insensitive).
  - public `HasStackIdentityMatch` — True when the text matched at least one weight-3 skill from an identity category.
  - public `ForeignStackDemands` — Foreign primary-stack demands found in the text, excluding stacks the operator has an…
  - public `FitScore` — 0–100 fit score mirroring the legacy stack tiers: a core-skill match scores like the…
  - public `PromptSummary` — Compact profile description for the AI triage prompt, strongest skills first.
  - public `SearchTerms` — Search keywords for connectors (bounty/issue queries): short, high-weight names first.
  - private `ContainsTerm` — Checks term. _(inferred)_
  - private `SplitAliases` — Transforms or resolves aliases. _(inferred)_
- **`SourceUrlCanonicalizer`** — Canonicalizes source URLs so the same post always yields the same string: drops the fragment (forum reply anchors like topic#post-123 point at the… (`src/DevLeads.Core/SourceUrlCanonicalizer.cs:8`)
  - public `Canonicalize` — Returns the canonical http(s) URL, or null when the input isn't one.
- **`EmergencyChecklist`** — Represents emergency checklist. _(inferred)_ (`src/DevLeads.Core/Templates/EmergencyChecklists.cs:3`)
- **`EmergencyChecklists`** — Diagnostic checklists surfaced when a lead becomes real work. (`src/DevLeads.Core/Templates/EmergencyChecklists.cs:6`)
  - public `SuggestFor` — Picks the most relevant checklist for a problem category.
- **`PricingTier`** — Represents pricing tier. _(inferred)_ (`src/DevLeads.Core/Templates/PricingTiers.cs:3`)
- **`PricingTiers`** — Suggested pricing tiers used by the quote generator and detail UI. (`src/DevLeads.Core/Templates/PricingTiers.cs:6`)
  - public `SuggestFor` — Chooses a tier from a category, returning a (min,max) suggested fee.
- **`ResponseTemplate`** — Represents response template. _(inferred)_ (`src/DevLeads.Core/Templates/ResponseTemplates.cs:3`)
- **`ResponseTemplates`** — Vetted response templates. Placeholders in [brackets] are filled per-opportunity. (`src/DevLeads.Core/Templates/ResponseTemplates.cs:6`)
  - public `Get` — Loads or resolves get. _(inferred)_
## DevLeads.Infrastructure

- **`AiTriageRouter`** — Registry of AI triage providers, selected by name from operator settings. All decision-making AI flows through here, so the backend is always… (`src/DevLeads.Infrastructure/Ai/AiTriageRouter.cs:13`)
  - public `Resolve` — The provider that will actually serve calls for these settings.
  - public `TriageAsync` — Coordinates triage. _(inferred)_
  - public `TriageBatchAsync` — Triages several posts in one model call when the resolved provider supports it.
  - public `ShortlistAsync` — Handles shortlist. _(inferred)_
  - private `BuildHeuristicShortlist` — Creates heuristic shortlist. _(inferred)_
- **`AnthropicTriageProvider : IAiTriageProvider`** — Calls Claude via the official Anthropic SDK with a single structured triage request. Returns strict JSON validated against AiTriageResult. (`src/DevLeads.Infrastructure/Ai/AnthropicTriageProvider.cs:14`)
  - public `IsAvailable` — Whether the provider can currently make calls (CLI present, key set, …).
  - public `AvailabilityMessage` — Human-readable explanation when IsAvailable is false.
  - public `TriageAsync` — Coordinates triage.
- **`HeuristicTriageProvider : IAiTriageProvider`** — A zero-cost, no-network triage provider. Infers a plausible structured result from keywords so the full pipeline runs end-to-end without an API key. (`src/DevLeads.Infrastructure/Ai/HeuristicTriageProvider.cs:12`)
  - public `IsAvailable` — Whether the provider can currently make calls (CLI present, key set, …).
  - public `AvailabilityMessage` — Human-readable explanation when IsAvailable is false.
  - private `IsPayIntent` — Job boards and hiring threads: the poster is already committed to paying.
  - public `TriageAsync` — Coordinates triage.
  - private `Classify` — Handles classify. _(inferred)_
  - private `DetectStack` — Handles detect stack. _(inferred)_
  - private `AddIf` — Creates if. _(inferred)_
  - private `BuildCause` — Creates cause. _(inferred)_
  - private `BuildStep` — Creates step. _(inferred)_
- **`OpenCodeTriageProvider : IAiTriageProvider, IAiBatchShortlistProvider, IAiBatchTriageProvider`** — Default AI provider: runs the single-pass structured triage through the local `opencode` CLI (https://opencode.ai). (`src/DevLeads.Infrastructure/Ai/OpenCodeTriageProvider.cs:18`)
  - public `IsAvailable` — Whether the provider can currently make calls (CLI present, key set, …).
  - public `AvailabilityMessage` — Human-readable explanation when IsAvailable is false.
  - public `TriageAsync` — Coordinates triage.
  - public `TriageBatchAsync` — Coordinates batch.
  - public `ShortlistAsync` — Handles shortlist.
  - public `GenerateTextAsync` — Generic long-form generation for the content studio: sends one prompt through the CLI and…
  - private `BuildPrompt` — Creates prompt. _(inferred)_
  - private `BuildShortlistPrompt` — Creates shortlist prompt. _(inferred)_
  - public `ResolveCliPath` — Resolves the configured CLI path, falling back to the standard install location.
  - private `OnPath` — Handles on path. _(inferred)_
  - private `Probe` — Handles probe. _(inferred)_
  - public `ResetProbe` — Clears the cached probe so a changed CLI path takes effect immediately.
  - private `RunWithModelFallbackAsync` — Runs the prompt against the configured model, then walks the fallback chain on any…
  - private `RunCliAsync` — Coordinates cli. _(inferred)_
  - private `StripAnsi` — Handles strip ansi. _(inferred)_
  - public `ExtractJsonObject` — Extracts the first balanced JSON object from arbitrary CLI output.
  - private `IsSchemaValid` — Checks schema valid. _(inferred)_
  - private `Normalize` — Coerces near-miss enum values back onto the strict schema instead of failing the call.
  - private `Truncate` — Handles truncate. _(inferred)_
- **`ShortlistOutput`** — Represents shortlist output. _(inferred)_ (`src/DevLeads.Infrastructure/Ai/OpenCodeTriageProvider.cs:433`)
- **`ShortlistSelection`** — Represents shortlist selection. _(inferred)_ (`src/DevLeads.Infrastructure/Ai/OpenCodeTriageProvider.cs:438`)
- **`ConnectorSupport`** — Shared helpers for connectors: content hashing and parameter parsing. (`src/DevLeads.Infrastructure/Connectors/ConnectorSupport.cs:8`)
  - public `ContentHash` — Stable hash used for duplicate detection across fetches.
  - public `NewItem` — Handles new item. _(inferred)_
- **`GitHubSearchConnector : ISourceConnector`** — Searches public GitHub issues for money-attached work: bounty-platform issues (BountyHub, Algora, IssueHunt and friends all anchor their bounties to… (`src/DevLeads.Infrastructure/Connectors/GitHubSearchConnector.cs:15`)
  - public `FetchAsync` — Loads or resolves fetch.
  - private `ParseIssue` — Transforms or resolves issue. _(inferred)_
  - private `CreateClient` — Creates client. _(inferred)_
  - private `GetInt` — Loads or resolves int. _(inferred)_
  - public `CheckHealthAsync` — Checks health.
- **`HackerNewsConnector : ISourceConnector`** — Discovers founder/operator pain via the Hacker News (Algolia) search API. (`src/DevLeads.Infrastructure/Connectors/HackerNewsConnector.cs:9`)
  - public `FetchAsync` — Loads or resolves fetch.
  - private `PickSearchTerms` — Handles pick search terms. _(inferred)_
  - private `GetInt` — Loads or resolves int. _(inferred)_
  - public `CheckHealthAsync` — Checks health.
- **`OpireConnector : ISourceConnector`** — Open bounties from Opire (https://opire.dev): money attached to public GitHub issues, paid out on merge. Public JSON API, no auth. (`src/DevLeads.Infrastructure/Connectors/OpireConnector.cs:13`)
  - public `FetchAsync` — Loads or resolves fetch.
  - private `ParseReward` — Transforms or resolves reward. _(inferred)_
  - private `GetInt` — Loads or resolves int. _(inferred)_
  - public `CheckHealthAsync` — Checks health.
- **`RedditConnector : ISourceConnector`** — Read-only ingestion of new posts from configured subreddits (manual response preferred). (`src/DevLeads.Infrastructure/Connectors/RedditConnector.cs:13`)
  - public `FetchAsync` — Loads or resolves fetch.
  - private `FetchListingAsync` — Fetches one subreddit feed. Returns false when rate-limited (callers stop the run).
  - private `StripHtml` — Handles strip html. _(inferred)_
  - private `PickSearchTerms` — Handles pick search terms. _(inferred)_
  - private `GetInt` — Loads or resolves int. _(inferred)_
  - public `CheckHealthAsync` — Checks health.
- **`RemotiveConnector : ISourceConnector`** — Real companies posting paid remote software work via the Remotive job API. Defaults to contract/freelance software roles — businesses actively… (`src/DevLeads.Infrastructure/Connectors/RemotiveConnector.cs:13`)
  - private `LooksLikeDevRole` — Handles looks like dev role. _(inferred)_
  - public `FetchAsync` — Loads or resolves fetch.
  - private `StripHtml` — Handles strip html. _(inferred)_
  - public `CheckHealthAsync` — Checks health.
- **`RssConnector : ISourceConnector`** — Simple, reliable ingestion of RSS / Atom feeds configured per source. (`src/DevLeads.Infrastructure/Connectors/RssConnector.cs:10`)
  - private `Feeds` — Handles feeds. _(inferred)_
  - public `FetchAsync` — Loads or resolves fetch.
  - private `StripHtml` — Handles strip html. _(inferred)_
  - private `EnrichForumThreadAsync` — Handles enrich forum thread. _(inferred)_
  - private `HasTruthyProperty` — Checks truthy property. _(inferred)_
  - private `GetDate` — Loads or resolves date. _(inferred)_
  - public `CheckHealthAsync` — Checks health.
- **`StackExchangeConnector : ISourceConnector`** — Fresh technical problem detection via the Stack Exchange API. (`src/DevLeads.Infrastructure/Connectors/StackExchangeConnector.cs:9`)
  - public `FetchAsync` — Loads or resolves fetch.
  - private `StripHtml` — Handles strip html. _(inferred)_
  - private `PickSearchTerms` — Handles pick search terms. _(inferred)_
  - private `GetInt` — Loads or resolves int. _(inferred)_
  - public `CheckHealthAsync` — Checks health.
- **`DatabaseSeeder`** — Creates the database and seeds query packs, source configs, and settings. Also migrates older databases: removes retired sources (GitHub Issues) and… (`src/DevLeads.Infrastructure/Data/DatabaseSeeder.cs:14`)
  - public `InitializeAsync` — Coordinates initialize. _(inferred)_
  - private `RequeueTemplateDraftsAsync` — One-time (2026-07-11): unapproved template mad-lib drafts ("I saw your post about…
  - private `ApplyStackIdentityCapsAsync` — Applies the stack-identity score cap (50, below Medium) to leads scored before the gate…
  - private `DemoteGenericCapabilitySkillsAsync` — One-time data fix (2026-07-11): "REST API" was seeded as a weight-3 "Primary stack" skill…
  - private `PurgeForeignStackLeadsAsync` — Removes discovery leads that demand a stack outside the operator's profile without…
  - private `ApplySchemaUpgradesAsync` — EnsureCreated never alters existing tables, so columns added after first release are…
  - private `MigrateAiProviderDefaultsAsync` — Moves settings still on an old AI default onto the current one (OpenCode CLI).
  - private `SeedQueryPacksAsync` — Creates query packs. _(inferred)_
  - private `SeedSkillsAsync` — Seeds the operator skill profile once; the Skills page owns it afterwards.
  - private `SeedCampaignsAsync` — Ensures the built-in campaigns exist (add-only: name/objective edits belong to the…
  - private `SeedSourceConfigsAsync` — Creates source configs. _(inferred)_
  - private `BackfillLeadCampaignsAsync` — Assigns campaign-less leads to their source's campaign (manual/unknown → emergency).
  - private `IsLegacyDefaultSource` — Detects configs still carrying earlier seeded defaults so we can upgrade them in place.
  - private `ApplySourceDefaults` — Reapplies seeded defaults, returning whether anything actually changed — a boot with…
  - private `DefaultSources` — Handles default sources. _(inferred)_
  - private `EmergencySources` — Default sources are chosen for commercial intent: places where a business owner, manager…
  - private `ModernizationSources` — Sources for the.NET legacy modernization consulting campaign.
  - private `SeedTrendSourcesAsync` — Content-studio trend sources (add-only; the operator owns them afterwards).
  - private `RssParams` — Handles rss params. _(inferred)_
  - private `RemoveRetiredSourcesAsync` — Deletes retired source configs and every item/lead they produced (e.g. GitHub Issues).
  - private `RemoveReplacedSourceConfigsAsync` — Removes old broad source config rows after splitting them into tuned variants.
  - private `PurgeStaleDiscoveryLeadsAsync` — One-time after a source-lineup migration: leads still sitting in triage stages were…
  - private `PurgeNonActionableLeadsAsync` — Purges leads that will not lead to financial compensation: pre-filter rejects, triage…
  - private `PurgeNonHirableVendorSupportLeadsAsync` — Removes or transitions non hirable vendor support leads. _(inferred)_
  - private `PurgeSourceLessLeadsAsync` — Every visible opportunity must point back to its original public source.
  - private `DeleteLeadsKeepDedupAsync` — Removes lead rows while detaching raw items, so dedup never re-ingests the same post.
- **`DevLeadsDbContext : DbContext`** — EF Core context for the SQLite solo database. (`src/DevLeads.Infrastructure/Data/DevLeadsDbContext.cs:9`)
  - protected `ConfigureConventions` — Handles configure conventions. _(inferred)_
  - protected `OnModelCreating` — Handles on model creating. _(inferred)_
- **`DateTimeOffsetToTicksConverter : ValueConverter<DateTimeOffset, long>`** — Represents date time offset to ticks converter. _(inferred)_ (`src/DevLeads.Infrastructure/Data/DevLeadsDbContext.cs:33`)
- **`DependencyInjection`** — Represents dependency injection. _(inferred)_ (`src/DevLeads.Infrastructure/DependencyInjection.cs:17`)
  - public `AddDevLeads` — Registers the database, connectors, AI providers, domain services, and worker.
  - public `InitializeDevLeadsAsync` — Creates the database schema and seeds default settings, query packs, and sources.
- **`DbQueryPackProvider : IQueryPackProvider`** — Loads query-pack terms from the database (cached per scope). (`src/DevLeads.Infrastructure/QueryPacks/DbQueryPackProvider.cs:7`)
  - private `Split` — Transforms or resolves split. _(inferred)_
  - public `GetTerms` — Returns the terms for a named pack (empty if unknown).
  - public `GetHighPriorityTerms` — All high-priority emergency terms across packs.
  - public `GetHighPriorityTerms` — All high-priority emergency terms across packs.
  - public `GetNegativeTerms` — All negative / exclusion terms.
- **`AuditService`** — Writes audit-trail entries for generated/sent messages and state changes. (`src/DevLeads.Infrastructure/Services/AuditService.cs:8`)
  - public `Record` — Handles record. _(inferred)_
- **`ContentStudioService`** — Turns trend signals into publishable output: AI-suggested topics, then full drafts (blog posts, articles, white/research papers, LinkedIn posts) for… (`src/DevLeads.Infrastructure/Services/ContentStudioService.cs:18`)
  - public `GenerateTopicsAsync` — One AI call: distills the hottest recent signals into up to maxTopics new topic suggestions.
  - public `GenerateDraftAsync` — One AI call: writes a full draft for a topic in the requested format.
  - private `SplitTitle` — The output contract is "# Title" on line one; fall back to the topic title.
  - private `Spaced` — Handles spaced. _(inferred)_
  - private `GetSettingsAsync` — Loads or resolves settings. _(inferred)_
- **`TopicOutput`** — Represents topic output. _(inferred)_ (`src/DevLeads.Infrastructure/Services/ContentStudioService.cs:199`)
- **`TopicSuggestion`** — Represents topic suggestion. _(inferred)_ (`src/DevLeads.Infrastructure/Services/ContentStudioService.cs:204`)
- **`DiscoveryActivityTracker`** — In-memory, app-wide record of what discovery is doing right now: which sources are mid-fetch and a rolling feed of recent events (runs, new leads… (`src/DevLeads.Infrastructure/Services/DiscoveryActivityTracker.cs:8`)
  - public `RunStarted` — Coordinates started. _(inferred)_
  - public `RunCompleted` — Coordinates completed. _(inferred)_
  - public `LeadCreated` — Handles lead created. _(inferred)_
  - public `Snapshot` — Handles snapshot. _(inferred)_
  - private `AddLocked` — Creates locked. _(inferred)_
- **`ActivityEvent`** — Represents activity event. _(inferred)_ (`src/DevLeads.Infrastructure/Services/DiscoveryActivityTracker.cs:10`)
- **`RunningSource`** — Represents running source. _(inferred)_ (`src/DevLeads.Infrastructure/Services/DiscoveryActivityTracker.cs:11`)
- **`LeadIngestionService`** — The two-step triage funnel: heuristic pre-filter, then (for survivors) a single structured AI call, followed by weighted scoring and optional draft… (`src/DevLeads.Infrastructure/Services/LeadIngestionService.cs:20`)
  - public `IngestAsync` — Runs a discovered item through the full pipeline. Returns null if a duplicate.
  - public `RecordRawOnlyAsync` — Records a fetched item as seen without creating an opportunity.
  - public `CreateManualAsync` — Manual lead entry that still runs the pre-filter, AI triage, and scoring.
  - public `RerunAsync` — Re-runs triage + scoring for an existing opportunity (used by the "rerun" endpoint).
  - private `RunTriageScoreAndDraftAsync` — Coordinates triage score and draft. _(inferred)_
  - private `DecideStatusAndDraft` — Handles decide status and draft. _(inferred)_
  - private `CreateDraft` — Creates draft. _(inferred)_
  - private `ApplyPreFilter` — Updates pre filter. _(inferred)_
  - private `ApplyAiResult` — Updates ai result. _(inferred)_
  - private `ApplyScore` — Updates score. _(inferred)_
  - public `MapRecommendation` — Transforms or resolves recommendation. _(inferred)_
  - private `GetSettingsAsync` — Loads or resolves settings. _(inferred)_
  - private `GetSkillsAsync` — Loads or resolves skills. _(inferred)_
  - private `GetCampaignObjectiveAsync` — Loads or resolves campaign objective. _(inferred)_
  - private `PackNames` — Handles pack names. _(inferred)_
  - private `ResolveTriageSettings` — Transforms or resolves triage settings. _(inferred)_
  - private `CloneWithProvider` — Handles clone with provider. _(inferred)_
  - public `IsOverAiBudgetAsync` — True when the count of real (non-heuristic) AI calls in the last hour has hit the cap.
  - private `GetSourceParameter` — Loads or resolves source parameter. _(inferred)_
  - private `GetBodyAsync` — Loads or resolves body. _(inferred)_
  - private `DeserializeList` — Handles deserialize list. _(inferred)_
  - private `NormalizeSourceUrl` — Transforms or resolves source url. _(inferred)_
  - private `FindNearDuplicateOpportunityAsync` — Loads or resolves near duplicate opportunity. _(inferred)_
  - private `Truncate` — Handles truncate. _(inferred)_
- **`MaintenanceService`** — Periodic housekeeping: stale-lead archiving and overdue-quote flagging. (`src/DevLeads.Infrastructure/Services/MaintenanceService.cs:9`)
  - public `ArchiveStaleLeadsAsync` — Removes or transitions stale leads. _(inferred)_
  - public `RejectNonHirableVendorSupportAsync` — Removes or transitions non hirable vendor support. _(inferred)_
  - public `FlagOverdueQuotesAsync` — Handles flag overdue quotes. _(inferred)_
  - public `DueFollowUpCountAsync` — Count of opportunities whose follow-up is now due (surfaced on the dashboard).
- **`OutreachService`** — Manages the human-in-the-loop outreach queue: drafts, approvals, sends, and suppression. (`src/DevLeads.Infrastructure/Services/OutreachService.cs:19`)
  - public `QueueForGenerationAsync` — Adds a lead to the AI generation queue. Idempotent: an existing queued/pending/ approved…
  - public `QueuedCountAsync` — Count of attempts currently waiting in the generation queue.
  - public `GenerateQueuedResponsesAsync` — Writes every queued reply in a single model call (chunked only past GenerationChunkSize…
  - private `ParseResponses` — Transforms or resolves responses. _(inferred)_
  - public `GenerateDraftAsync` — Creates draft. _(inferred)_
  - public `ApproveAsync` — Handles approve. _(inferred)_
  - public `SendAsync` — "Sends" the outreach. In this MVP this records a Gmail draft / manual send and marks the…
  - public `CancelAsync` — Removes or transitions cancel. _(inferred)_
  - public `MarkRespondedAsync` — Updates responded. _(inferred)_
  - public `IsSuppressedAsync` — Checks suppressed. _(inferred)_
  - public `AddSuppressionAsync` — Creates suppression. _(inferred)_
  - private `GetSettings` — Loads or resolves settings. _(inferred)_
  - private `Get` — Loads or resolves get. _(inferred)_
- **`QuoteService`** — Quote generation and payment-state tracking for bounded emergency fixes. (`src/DevLeads.Infrastructure/Services/QuoteService.cs:10`)
  - public `GenerateAsync` — Creates generate. _(inferred)_
  - public `SendAsync` — Handles send. _(inferred)_
  - public `MarkPaidAsync` — Updates paid. _(inferred)_
  - public `MarkOverdueAsync` — Updates overdue. _(inferred)_
  - private `Get` — Loads or resolves get. _(inferred)_
- **`SourceRunner`** — Runs one source end-to-end: fetch via its connector, then ingest each item. (`src/DevLeads.Infrastructure/Services/SourceRunner.cs:15`)
  - public `RunAsync` — Fetches and ingests for a single source config. Returns the number of new opportunities.
  - private `GetSeenExternalIdsAsync` — Which of these items' external ids are already recorded for this source.
  - private `BatchTriageAsync` — Triages the shortlist survivors in chunked batch calls (one model call per…
  - private `BuildShortlistGateAsync` — Creates shortlist gate. _(inferred)_
  - private `ShouldUseBatchShortlist` — Checks use batch shortlist. _(inferred)_
  - private `ResolveShortlistMax` — Transforms or resolves shortlist max. _(inferred)_
  - private `ResolveTriageProvider` — Transforms or resolves triage provider. _(inferred)_
  - private `ResolveTriageSettings` — Transforms or resolves triage settings. _(inferred)_
  - private `BuildRunMessage` — Creates run message. _(inferred)_
  - private `BuildTerms` — Creates terms. _(inferred)_
  - private `PackNames` — Handles pack names. _(inferred)_
  - private `GetCampaignObjectiveAsync` — Loads or resolves campaign objective. _(inferred)_
  - private `ParseParameters` — Transforms or resolves parameters. _(inferred)_
  - private `GetBool` — Loads or resolves bool. _(inferred)_
  - private `GetInt` — Loads or resolves int. _(inferred)_
  - public `CheckHealthAsync` — Checks health. _(inferred)_
  - private `ResolveConnectorKey` — Transforms or resolves connector key. _(inferred)_
  - private `TrimJsonString` — Transforms or resolves json string. _(inferred)_
  - private `Compact` — Transforms or resolves compact. _(inferred)_
- **`ShortlistGate`** — Represents shortlist gate. _(inferred)_ (`src/DevLeads.Infrastructure/Services/SourceRunner.cs:413`)
  - public `ShouldRecordRawOnly` — Checks record raw only. _(inferred)_
- **`TrendScanService`** — Polls trend sources (release feeds, vendor blogs, HN, subreddits) and stores skill-relevant items as TrendSignals ranked by hotness. (`src/DevLeads.Infrastructure/Services/TrendScanService.cs:16`)
  - public `RunDueAsync` — Runs every enabled trend source that is due. Returns new signal count.
  - public `RunSourceAsync` — Coordinates source. _(inferred)_
  - private `ComputeHotness` — Skill relevance dominates; platform engagement and freshness break ties.
  - private `ExtractEngagement` — HN Algolia hits carry points/num_comments in the raw JSON; other feeds don't.
  - private `GetSeenExternalIdsAsync` — Loads or resolves seen external ids. _(inferred)_
  - private `PruneOldSignalsAsync` — Trend evidence goes stale fast; anything past 30 days is dead weight.
  - private `ParseParameters` — Transforms or resolves parameters. _(inferred)_
  - private `Compact` — Transforms or resolves compact. _(inferred)_
- **`ContentTrendWorker : BackgroundService`** — Slow background loop for the content studio: polls due trend sources (default twice a day per source) and, at most once a day, spends one AI call… (`src/DevLeads.Infrastructure/Workers/ContentTrendWorker.cs:16`)
  - protected `ExecuteAsync` — Coordinates execute. _(inferred)_
  - private `TickAsync` — Handles tick. _(inferred)_
  - private `MaybeSuggestTopicsAsync` — One automatic topic-suggestion call per day, and only when there is fresh material to work…
- **`DiscoveryWorker : BackgroundService`** — The core background loop. Every minute it runs any sources that are due (respecting each source's poll interval), and hourly it runs maintenance. (`src/DevLeads.Infrastructure/Workers/DiscoveryWorker.cs:15`)
  - protected `ExecuteAsync` — Coordinates execute. _(inferred)_
  - private `TickAsync` — Handles tick. _(inferred)_
## DevLeads.Web

- **`ApiEndpoints`** — Internal HTTP API used for automation and integration (the UI calls services directly). (`src/DevLeads.Web/Api/ApiEndpoints.cs:9`)
  - public `MapDevLeadsApi` — Transforms or resolves dev leads api. _(inferred)_
  - private `MapStatusAction` — Transforms or resolves status action. _(inferred)_
- **`ManualLeadDto`** — Transfers manual lead data. _(inferred)_ (`src/DevLeads.Web/Api/ApiEndpoints.cs:176`)
- **`DraftDto`** — Transfers draft data. _(inferred)_ (`src/DevLeads.Web/Api/ApiEndpoints.cs:177`)
- **`QuoteDto`** — Transfers quote data. _(inferred)_ (`src/DevLeads.Web/Api/ApiEndpoints.cs:178`)
- **`AppRestartService`** — Full-process restart so the app picks up the latest code. Spawns a detached supervisor script that waits for this process to exit, rebuilds the… (`src/DevLeads.Web/AppRestartService.cs:12`)
  - public `Restart` — Schedules the restart. Returns an error message, or null when underway.
- **`App : ComponentBase`** — Blazor component for app. (`src/DevLeads.Web/Components/App.razor:1`)
- **`MainLayout : LayoutComponentBase`** — Blazor component for main layout. (`src/DevLeads.Web/Components/Layout/MainLayout.razor:1`)
- **`NavMenu : ComponentBase`** — Blazor component for nav menu. (`src/DevLeads.Web/Components/Layout/NavMenu.razor:1`)
- **`ReconnectModal : ComponentBase`** — Blazor component for reconnect modal. (`src/DevLeads.Web/Components/Layout/ReconnectModal.razor:1`)
  - module `handleReconnectStateChanged` — Handles handle reconnect state changed. _(inferred)_
  - module `retry` — Handles retry. _(inferred)_
  - module `resume` — Handles resume. _(inferred)_
  - module `retryWhenDocumentBecomesVisible` — Handles retry when document becomes visible. _(inferred)_
- **`Campaigns : ComponentBase`** — Campaign objectives and source/lead ownership management. (`src/DevLeads.Web/Components/Pages/Campaigns.razor:1`)
  - protected `OnInitializedAsync` — Runs the component on initialized lifecycle step. _(inferred)_
  - private `Load` — Loads or resolves load. _(inferred)_
  - private `Save` — Updates save. _(inferred)_
  - private `Create` — Creates create. _(inferred)_
  - private `Delete` — Removes or transitions delete. _(inferred)_
  - private `MakeKey` — Creates key. _(inferred)_
- **`Content : ComponentBase`** — Trend signals, suggested topics, and publishable draft management. (`src/DevLeads.Web/Components/Pages/Content.razor:1`)
  - protected `OnInitializedAsync` — Runs the component on initialized lifecycle step. _(inferred)_
  - private `Load` — Loads or resolves load. _(inferred)_
  - private `ScanNow` — Coordinates now. _(inferred)_
  - private `SuggestTopics` — Handles suggest topics. _(inferred)_
  - private `Draft` — Handles draft. _(inferred)_
  - private `Run` — Coordinates run. _(inferred)_
  - private `DismissTopic` — Removes or transitions topic. _(inferred)_
  - private `ToggleEditor` — Handles editor. _(inferred)_
  - private `SaveDraft` — Updates draft. _(inferred)_
  - private `SetDraftStatus` — Updates draft status. _(inferred)_
  - private `Copy` — Handles copy. _(inferred)_
  - private `DraftChip` — Handles draft chip. _(inferred)_
  - private `FormatLabel` — Transforms or resolves label. _(inferred)_
  - private `Shorten` — Handles shorten. _(inferred)_
- **`Drafts : ComponentBase`** — Outreach generation and human approval queues. (`src/DevLeads.Web/Components/Pages/Drafts.razor:1`)
- **`Error : ComponentBase`** — Unhandled-error page. (`src/DevLeads.Web/Components/Pages/Error.razor:1`)
- **`Home : ComponentBase`** — Campaign-scoped dashboard with lead KPIs, activity, and top opportunities. (`src/DevLeads.Web/Components/Pages/Home.razor:1`)
  - private `Truncate` — Handles truncate. _(inferred)_
  - protected `OnInitializedAsync` — Runs the component on initialized lifecycle step. _(inferred)_
  - private `IsDashboardLead` — Checks dashboard lead. _(inferred)_
  - private `DashboardDuplicateKey` — Handles dashboard duplicate key. _(inferred)_
- **`NewOpportunity : ComponentBase`** — Manual lead entry through the normal triage pipeline. (`src/DevLeads.Web/Components/Pages/NewOpportunity.razor:1`)
  - protected `OnInitializedAsync` — Runs the component on initialized lifecycle step. _(inferred)_
  - private `Create` — Creates create. _(inferred)_
- **`NotFound : ComponentBase`** — Missing-route page. (`src/DevLeads.Web/Components/Pages/NotFound.razor:1`)
- **`Opportunities : ComponentBase`** — Searchable and filterable lead-review queue. (`src/DevLeads.Web/Components/Pages/Opportunities.razor:1`)
  - private `if` — Handles if. _(inferred)_
  - protected `OnInitializedAsync` — Runs the component on initialized lifecycle step. _(inferred)_
  - private `SetFilter` — Updates filter. _(inferred)_
  - private `Archive` — Removes or transitions archive. _(inferred)_
  - private `Restore` — Handles restore. _(inferred)_
  - private `QueueResponse` — Handles response. _(inferred)_
  - private `SetStatus` — Updates status. _(inferred)_
  - private `Apply` — Updates apply. _(inferred)_
  - private `Sort` — Handles sort. _(inferred)_
  - private `SearchText` — Handles search text. _(inferred)_
  - private `MatchesAny` — Handles matches any. _(inferred)_
  - private `Spaced` — Handles spaced. _(inferred)_
- **`OpportunityDetail : ComponentBase`** — Lead detail, triage, scoring, outreach, quotes, work tracking, and audit history. (`src/DevLeads.Web/Components/Pages/OpportunityDetail.razor:1`)
  - protected `OnParametersSetAsync` — Runs the component on parameters set lifecycle step. _(inferred)_
  - private `Load` — Loads or resolves load. _(inferred)_
  - private `LoadChecklist` — Loads or resolves checklist. _(inferred)_
  - private `RunScoped` — Coordinates scoped. _(inferred)_
  - private `Rerun` — Handles rerun. _(inferred)_
  - private `Status` — Handles status. _(inferred)_
  - private `GenerateDraft` — Creates draft. _(inferred)_
  - private `QueueResponse` — Handles response. _(inferred)_
  - private `SaveDraft` — Updates draft. _(inferred)_
  - private `ApproveDraft` — Handles draft. _(inferred)_
  - private `SendDraft` — Handles draft. _(inferred)_
  - private `CancelDraft` — Removes or transitions draft. _(inferred)_
  - private `GenerateQuote` — Creates quote. _(inferred)_
  - private `SendQuote` — Handles quote. _(inferred)_
  - private `MarkPaid` — Updates paid. _(inferred)_
  - private `StartWork` — Handles start work. _(inferred)_
  - private `ToggleChecklist` — Handles checklist. _(inferred)_
  - private `CopyText` — Handles text. _(inferred)_
  - private `FixTime` — Handles fix time. _(inferred)_
  - private `Bar` — Handles bar. _(inferred)_
  - private `OppFromOutreach` — Handles opp from outreach. _(inferred)_
- **`Quotes : ComponentBase`** — Quote and payment-state management. (`src/DevLeads.Web/Components/Pages/Quotes.razor:1`)
- **`Settings : ComponentBase`** — Operator, AI, safety, discovery, and restart settings. (`src/DevLeads.Web/Components/Pages/Settings.razor:1`)
  - private `RestartServer` — Handles server. _(inferred)_
  - protected `OnInitializedAsync` — Runs the component on initialized lifecycle step. _(inferred)_
  - private `Save` — Updates save. _(inferred)_
  - private `OnProviderChanged` — Handles on provider changed. _(inferred)_
  - private `OnOpenCodeCliPathChanged` — Handles on open code cli path changed. _(inferred)_
- **`SkillProfile : ComponentBase`** — Operator skill-profile management. (`src/DevLeads.Web/Components/Pages/SkillProfile.razor:1`)
- **`Sources : ComponentBase`** — Source configuration, health checks, and manual discovery runs. (`src/DevLeads.Web/Components/Pages/Sources.razor:1`)
- **`Routes : ComponentBase`** — Blazor component for routes. (`src/DevLeads.Web/Components/Routes.razor:1`)
- **`ActivityFeed : ComponentBase, IDisposable`** — Blazor component for activity feed. (`src/DevLeads.Web/Components/Shared/ActivityFeed.razor:1`)
  - protected `OnInitializedAsync` — Runs the component on initialized lifecycle step. _(inferred)_
  - protected `OnAfterRender` — Runs the component on after render lifecycle step. _(inferred)_
  - private `PollLoopAsync` — Handles poll loop. _(inferred)_
  - private `RefreshAsync` — Handles refresh. _(inferred)_
  - private `Elapsed` — Handles elapsed. _(inferred)_
  - private `Due` — Handles due. _(inferred)_
  - private `KindChip` — Handles kind chip. _(inferred)_
  - private `KindLabel` — Handles kind label. _(inferred)_
  - public `Dispose` — Handles dispose. _(inferred)_
- **`CampaignSwitcher : ComponentBase`** — Blazor component for campaign switcher. (`src/DevLeads.Web/Components/Shared/CampaignSwitcher.razor:1`)
  - protected `OnInitializedAsync` — Runs the component on initialized lifecycle step. _(inferred)_
  - private `OnChanged` — Handles on changed. _(inferred)_
- **`UiHelpers`** — Presentation helpers: badge classes, labels, and formatting used across pages. (`src/DevLeads.Web/Components/Shared/UiHelpers.cs:8`)
  - public `PriorityClass` — Handles priority class. _(inferred)_
  - public `StatusClass` — Handles status class. _(inferred)_
  - public `AiStatusClass` — Handles ai status class. _(inferred)_
  - public `Spaced` — Handles spaced. _(inferred)_
  - public `Age` — Handles age. _(inferred)_
  - public `AgeClass` — Freshness badge: green < 1 day, yellow 1–3 days, red 3+ days.
  - public `CompensationOffered` — Did the author indicate they'd pay someone? True on an explicit pay-intent verdict or…
  - public `YesNo` — Yes/No/— chip for tri-state judgments.
  - public `Fee` — Handles fee. _(inferred)_
  - public `Fee` — Fee with provenance: an amount the poster stated is fact ("$15 offered"); a category-based…
  - public `ParseStringList` — Transforms or resolves string list. _(inferred)_
- **`_Imports : ComponentBase`** — Blazor component for imports. (`src/DevLeads.Web/Components/_Imports.razor:1`)


---

## Completeness

- 132 source-authored C# types and Razor components.
- 384 source-authored callable members.
- 13 Blazor page routes; 36 HTTP endpoints; 17 EF DbSets.
- Full indexed source: 563,775 characters (~140,944 tokens).
