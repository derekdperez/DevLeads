# AI Context Compact

Read this file first. Do not open implementation files until you have narrowed the task to a specific file, symbol, route, or registration.

## Agent rule

Use this generated context as the project map. Implementation bodies are intentionally omitted to reduce token usage.

---

# Project Map

Root: `/home/user/repo/UrgentLeads`

## Summary

- Indexed files: 80
- Indexed lines: 8,075
- Public/high-level symbols: 459

## File types

- `.cs`: 57
- `.csproj`: 3
- `.json`: 3
- `.razor`: 17

## Files

- `src/DevLeads.Core/Ai/AiTriagePrompts.cs` — 113 lines
- `src/DevLeads.Core/Ai/IAiTriageProvider.cs` — 90 lines
- `src/DevLeads.Core/AiTriageResult.cs` — 78 lines
- `src/DevLeads.Core/Connectors/ISourceConnector.cs` — 42 lines
- `src/DevLeads.Core/DevLeads.Core.csproj` — 10 lines
- `src/DevLeads.Core/Entities/AiTriageRun.cs` — 23 lines
- `src/DevLeads.Core/Entities/AuditEvent.cs` — 15 lines
- `src/DevLeads.Core/Entities/OperatorSettings.cs` — 61 lines
- `src/DevLeads.Core/Entities/Opportunity.cs` — 81 lines
- `src/DevLeads.Core/Entities/OutreachAttempt.cs` — 27 lines
- `src/DevLeads.Core/Entities/QueryPack.cs` — 17 lines
- `src/DevLeads.Core/Entities/Quote.cs` — 26 lines
- `src/DevLeads.Core/Entities/RawSourceItem.cs` — 30 lines
- `src/DevLeads.Core/Entities/Skill.cs` — 30 lines
- `src/DevLeads.Core/Entities/SourceConfig.cs` — 34 lines
- `src/DevLeads.Core/Entities/SuppressionEntry.cs` — 13 lines
- `src/DevLeads.Core/Entities/WorkSession.cs` — 21 lines
- `src/DevLeads.Core/Enums.cs` — 128 lines
- `src/DevLeads.Core/HeuristicPreFilter.cs` — 187 lines
- `src/DevLeads.Core/OfferedCompensation.cs` — 55 lines
- `src/DevLeads.Core/PreFilterResult.cs` — 16 lines
- `src/DevLeads.Core/QueryPacks/DefaultQueryPacks.cs` — 151 lines
- `src/DevLeads.Core/QueryPacks/IQueryPackProvider.cs` — 15 lines
- `src/DevLeads.Core/RedFlagDetector.cs` — 53 lines
- `src/DevLeads.Core/Scoring/OpportunityScorer.cs` — 304 lines
- `src/DevLeads.Core/Skills/DefaultSkills.cs` — 78 lines
- `src/DevLeads.Core/Skills/SkillMatcher.cs` — 68 lines
- `src/DevLeads.Core/SourceUrlCanonicalizer.cs` — 27 lines
- `src/DevLeads.Core/Templates/EmergencyChecklists.cs` — 38 lines
- `src/DevLeads.Core/Templates/PricingTiers.cs` — 29 lines
- `src/DevLeads.Core/Templates/ResponseTemplates.cs` — 57 lines
- `src/DevLeads.Infrastructure/Ai/AiTriageRouter.cs` — 129 lines
- `src/DevLeads.Infrastructure/Ai/AnthropicTriageProvider.cs` — 133 lines
- `src/DevLeads.Infrastructure/Ai/HeuristicTriageProvider.cs` — 293 lines
- `src/DevLeads.Infrastructure/Ai/OpenCodeTriageProvider.cs` — 446 lines
- `src/DevLeads.Infrastructure/Connectors/ConnectorSupport.cs` — 37 lines
- `src/DevLeads.Infrastructure/Connectors/GitHubSearchConnector.cs` — 157 lines
- `src/DevLeads.Infrastructure/Connectors/HackerNewsConnector.cs` — 85 lines
- `src/DevLeads.Infrastructure/Connectors/OpireConnector.cs` — 131 lines
- `src/DevLeads.Infrastructure/Connectors/RedditConnector.cs` — 147 lines
- `src/DevLeads.Infrastructure/Connectors/RemotiveConnector.cs` — 115 lines
- `src/DevLeads.Infrastructure/Connectors/RssConnector.cs` — 106 lines
- `src/DevLeads.Infrastructure/Connectors/StackExchangeConnector.cs` — 112 lines
- `src/DevLeads.Infrastructure/Data/DatabaseSeeder.cs` — 562 lines
- `src/DevLeads.Infrastructure/Data/DevLeadsDbContext.cs` — 76 lines
- `src/DevLeads.Infrastructure/DependencyInjection.cs` — 86 lines
- `src/DevLeads.Infrastructure/DevLeads.Infrastructure.csproj` — 21 lines
- `src/DevLeads.Infrastructure/QueryPacks/DbQueryPackProvider.cs` — 33 lines
- `src/DevLeads.Infrastructure/Services/AuditService.cs` — 28 lines
- `src/DevLeads.Infrastructure/Services/LeadIngestionService.cs` — 546 lines
- `src/DevLeads.Infrastructure/Services/MaintenanceService.cs` — 64 lines
- `src/DevLeads.Infrastructure/Services/OutreachService.cs` — 141 lines
- `src/DevLeads.Infrastructure/Services/QuoteService.cs` — 82 lines
- `src/DevLeads.Infrastructure/Services/SourceRunner.cs` — 316 lines
- `src/DevLeads.Infrastructure/Workers/DiscoveryWorker.cs` — 79 lines
- `src/DevLeads.Web/Api/ApiEndpoints.cs` — 144 lines
- `src/DevLeads.Web/AppRestartService.cs` — 100 lines
- `src/DevLeads.Web/Components/App.razor` — 23 lines
- `src/DevLeads.Web/Components/Layout/MainLayout.razor` — 17 lines
- `src/DevLeads.Web/Components/Layout/NavMenu.razor` — 25 lines
- `src/DevLeads.Web/Components/Layout/ReconnectModal.razor` — 32 lines
- `src/DevLeads.Web/Components/Pages/Drafts.razor` — 95 lines
- `src/DevLeads.Web/Components/Pages/Error.razor` — 37 lines
- `src/DevLeads.Web/Components/Pages/Home.razor` — 150 lines
- `src/DevLeads.Web/Components/Pages/NewOpportunity.razor` — 72 lines
- `src/DevLeads.Web/Components/Pages/NotFound.razor` — 5 lines
- `src/DevLeads.Web/Components/Pages/Opportunities.razor` — 237 lines
- `src/DevLeads.Web/Components/Pages/OpportunityDetail.razor` — 384 lines
- `src/DevLeads.Web/Components/Pages/Quotes.razor` — 97 lines
- `src/DevLeads.Web/Components/Pages/Settings.razor` — 218 lines
- `src/DevLeads.Web/Components/Pages/SkillProfile.razor` — 162 lines
- `src/DevLeads.Web/Components/Pages/Sources.razor` — 167 lines
- `src/DevLeads.Web/Components/Routes.razor` — 7 lines
- `src/DevLeads.Web/Components/Shared/UiHelpers.cs` — 106 lines
- `src/DevLeads.Web/Components/_Imports.razor` — 23 lines
- `src/DevLeads.Web/DevLeads.Web.csproj` — 40 lines
- `src/DevLeads.Web/Program.cs` — 49 lines
- `src/DevLeads.Web/Properties/launchSettings.json` — 24 lines
- `src/DevLeads.Web/appsettings.Development.json` — 9 lines
- `src/DevLeads.Web/appsettings.json` — 10 lines

---

# Entry Points

Read these before opening random implementation files.

| File/Folder | Why it matters |
| --- | --- |
| src/DevLeads.Core/DevLeads.Core.csproj | Project file; shows target framework and dependencies. |
| src/DevLeads.Infrastructure/DevLeads.Infrastructure.csproj | Project file; shows target framework and dependencies. |
| src/DevLeads.Web/Components/App.razor | UI/page/component folder. |
| src/DevLeads.Web/Components/Layout/MainLayout.razor | UI/page/component folder. |
| src/DevLeads.Web/Components/Layout/NavMenu.razor | UI/page/component folder. |
| src/DevLeads.Web/Components/Layout/ReconnectModal.razor | UI/page/component folder. |
| src/DevLeads.Web/Components/Pages/Drafts.razor | UI/page/component folder. |
| src/DevLeads.Web/Components/Pages/Error.razor | UI/page/component folder. |
| src/DevLeads.Web/Components/Pages/Home.razor | UI/page/component folder. |
| src/DevLeads.Web/Components/Pages/NewOpportunity.razor | UI/page/component folder. |
| src/DevLeads.Web/Components/Pages/NotFound.razor | UI/page/component folder. |
| src/DevLeads.Web/Components/Pages/Opportunities.razor | UI/page/component folder. |
| src/DevLeads.Web/Components/Pages/OpportunityDetail.razor | UI/page/component folder. |
| src/DevLeads.Web/Components/Pages/Quotes.razor | UI/page/component folder. |
| src/DevLeads.Web/Components/Pages/Settings.razor | UI/page/component folder. |
| src/DevLeads.Web/Components/Pages/SkillProfile.razor | UI/page/component folder. |
| src/DevLeads.Web/Components/Pages/Sources.razor | UI/page/component folder. |
| src/DevLeads.Web/Components/Routes.razor | UI/page/component folder. |
| src/DevLeads.Web/Components/Shared/UiHelpers.cs | UI/page/component folder. |
| src/DevLeads.Web/Components/_Imports.razor | UI/page/component folder. |
| src/DevLeads.Web/DevLeads.Web.csproj | Project file; shows target framework and dependencies. |
| src/DevLeads.Web/Program.cs | Main ASP.NET/Core startup or console entry point. |
| src/DevLeads.Web/appsettings.Development.json | Application configuration; values should be redacted before sharing. |
| src/DevLeads.Web/appsettings.json | Application configuration; values should be redacted before sharing. |

---

# Dependencies

## Solutions

_None found._

## .NET projects

### `src/DevLeads.Core/DevLeads.Core.csproj`
- SDK: `Microsoft.NET.Sdk`
- Target frameworks: `net10.0`

### `src/DevLeads.Infrastructure/DevLeads.Infrastructure.csproj`
- SDK: `Microsoft.NET.Sdk`
- Target frameworks: `net10.0`
- Packages:
  - `Anthropic 12.35.1`
  - `Microsoft.EntityFrameworkCore.Sqlite 10.0.9`
  - `Microsoft.Extensions.Hosting.Abstractions 10.0.9`
  - `Microsoft.Extensions.Http 10.0.9`
- Project references:
  - `../DevLeads.Core/DevLeads.Core.csproj`

### `src/DevLeads.Web/DevLeads.Web.csproj`
- SDK: `Microsoft.NET.Sdk.Web`
- Target frameworks: `net10.0`
- Project references:
  - `../DevLeads.Infrastructure/DevLeads.Infrastructure.csproj`


## package.json

_None found._

---

# Routes, Dependency Injection, and EF Core

## API Routes

| Verb | Route | File | Line | Method |
| --- | --- | --- | --- | --- |
| GET | /favicon.ico | src/DevLeads.Web/Program.cs | 43 | minimal-api |


## Dependency Injection

| Lifetime | Service | Implementation | File | Line |
| --- | --- | --- | --- | --- |
| Scoped | DevLeadsDbContext |  | src/DevLeads.Infrastructure/DependencyInjection.cs | 25 |
| Transient | ISourceConnector | RssConnector | src/DevLeads.Infrastructure/DependencyInjection.cs | 41 |
| Transient | ISourceConnector | HackerNewsConnector | src/DevLeads.Infrastructure/DependencyInjection.cs | 42 |
| Transient | ISourceConnector | StackExchangeConnector | src/DevLeads.Infrastructure/DependencyInjection.cs | 43 |
| Transient | ISourceConnector | RemotiveConnector | src/DevLeads.Infrastructure/DependencyInjection.cs | 44 |
| Transient | ISourceConnector | RedditConnector | src/DevLeads.Infrastructure/DependencyInjection.cs | 45 |
| Transient | ISourceConnector | OpireConnector | src/DevLeads.Infrastructure/DependencyInjection.cs | 46 |
| Transient | ISourceConnector | GitHubSearchConnector | src/DevLeads.Infrastructure/DependencyInjection.cs | 47 |
| Scoped | IQueryPackProvider | DbQueryPackProvider | src/DevLeads.Infrastructure/DependencyInjection.cs | 50 |
| Scoped | HeuristicPreFilter |  | src/DevLeads.Infrastructure/DependencyInjection.cs | 51 |
| Singleton | HeuristicTriageProvider |  | src/DevLeads.Infrastructure/DependencyInjection.cs | 56 |
| Singleton | AnthropicTriageProvider |  | src/DevLeads.Infrastructure/DependencyInjection.cs | 57 |
| Singleton | OpenCodeTriageProvider |  | src/DevLeads.Infrastructure/DependencyInjection.cs | 58 |
| Singleton | IAiTriageProvider |  | src/DevLeads.Infrastructure/DependencyInjection.cs | 59 |
| Singleton | IAiTriageProvider |  | src/DevLeads.Infrastructure/DependencyInjection.cs | 60 |
| Singleton | IAiTriageProvider |  | src/DevLeads.Infrastructure/DependencyInjection.cs | 61 |
| Singleton | AiTriageRouter |  | src/DevLeads.Infrastructure/DependencyInjection.cs | 62 |
| Scoped | AuditService |  | src/DevLeads.Infrastructure/DependencyInjection.cs | 65 |
| Scoped | LeadIngestionService |  | src/DevLeads.Infrastructure/DependencyInjection.cs | 66 |
| Scoped | OutreachService |  | src/DevLeads.Infrastructure/DependencyInjection.cs | 67 |
| Scoped | QuoteService |  | src/DevLeads.Infrastructure/DependencyInjection.cs | 68 |
| Scoped | SourceRunner |  | src/DevLeads.Infrastructure/DependencyInjection.cs | 69 |
| Scoped | MaintenanceService |  | src/DevLeads.Infrastructure/DependencyInjection.cs | 70 |
| Singleton | DevLeads.Web.AppRestartService |  | src/DevLeads.Web/Program.cs | 25 |


## EF Core DbSets

_None found._

---

# Public API Surface

Only public/high-level symbols are listed. Implementation bodies are intentionally omitted.

| File | Line | Kind | Signature |
| --- | --- | --- | --- |
| src/DevLeads.Core/Ai/AiTriagePrompts.cs | 2 | class | public static class AiTriagePrompts |
| src/DevLeads.Core/Ai/AiTriagePrompts.cs | 57 | method | public static string BuildUserPrompt(AiTriageRequest r) |
| src/DevLeads.Core/Ai/IAiTriageProvider.cs | 4 | class | public sealed class AiTriageRequest |
| src/DevLeads.Core/Ai/IAiTriageProvider.cs | 8 | property | public string Title { get; } |
| src/DevLeads.Core/Ai/IAiTriageProvider.cs | 9 | property | public string Body { get; } |
| src/DevLeads.Core/Ai/IAiTriageProvider.cs | 10 | property | public string SourceKey { get; } |
| src/DevLeads.Core/Ai/IAiTriageProvider.cs | 11 | property | public DateTimeOffset PostedAt { get; } |
| src/DevLeads.Core/Ai/IAiTriageProvider.cs | 12 | property | public IReadOnlyList<string> MatchedTerms { get; } |
| src/DevLeads.Core/Ai/IAiTriageProvider.cs | 13 | property | public decimal HeuristicScore { get; } |
| src/DevLeads.Core/Ai/IAiTriageProvider.cs | 14 | property | public string OperatorSkills { get; } |
| src/DevLeads.Core/Ai/IAiTriageProvider.cs | 18 | class | public sealed class AiTriageResponse |
| src/DevLeads.Core/Ai/IAiTriageProvider.cs | 22 | property | public bool Succeeded { get; } |
| src/DevLeads.Core/Ai/IAiTriageProvider.cs | 23 | property | public AiTriageResult? Result { get; } |
| src/DevLeads.Core/Ai/IAiTriageProvider.cs | 24 | property | public string Provider { get; } |
| src/DevLeads.Core/Ai/IAiTriageProvider.cs | 25 | property | public string Model { get; } |
| src/DevLeads.Core/Ai/IAiTriageProvider.cs | 26 | property | public string RequestJson { get; } |
| src/DevLeads.Core/Ai/IAiTriageProvider.cs | 27 | property | public string? ResponseJson { get; } |
| src/DevLeads.Core/Ai/IAiTriageProvider.cs | 28 | property | public string? ErrorMessage { get; } |
| src/DevLeads.Core/Ai/IAiTriageProvider.cs | 29 | property | public bool Retryable { get; } |
| src/DevLeads.Core/Ai/IAiTriageProvider.cs | 31 | class | public sealed class AiShortlistItem |
| src/DevLeads.Core/Ai/IAiTriageProvider.cs | 35 | property | public string Id { get; } |
| src/DevLeads.Core/Ai/IAiTriageProvider.cs | 36 | property | public string Title { get; } |
| src/DevLeads.Core/Ai/IAiTriageProvider.cs | 37 | property | public string Snippet { get; } |
| src/DevLeads.Core/Ai/IAiTriageProvider.cs | 38 | property | public string SourceKey { get; } |
| src/DevLeads.Core/Ai/IAiTriageProvider.cs | 39 | property | public DateTimeOffset PostedAt { get; } |
| src/DevLeads.Core/Ai/IAiTriageProvider.cs | 40 | property | public IReadOnlyList<string> MatchedTerms { get; } |
| src/DevLeads.Core/Ai/IAiTriageProvider.cs | 41 | property | public decimal HeuristicScore { get; } |
| src/DevLeads.Core/Ai/IAiTriageProvider.cs | 43 | class | public sealed class AiShortlistDecision |
| src/DevLeads.Core/Ai/IAiTriageProvider.cs | 46 | property | public string Id { get; } |
| src/DevLeads.Core/Ai/IAiTriageProvider.cs | 47 | property | public bool ShouldTriage { get; } |
| src/DevLeads.Core/Ai/IAiTriageProvider.cs | 48 | property | public string Reason { get; } |
| src/DevLeads.Core/Ai/IAiTriageProvider.cs | 50 | class | public sealed class AiShortlistResponse |
| src/DevLeads.Core/Ai/IAiTriageProvider.cs | 53 | property | public bool Succeeded { get; } |
| src/DevLeads.Core/Ai/IAiTriageProvider.cs | 54 | property | public IReadOnlyList<AiShortlistDecision> Decisions { get; } |
| src/DevLeads.Core/Ai/IAiTriageProvider.cs | 55 | property | public string Provider { get; } |
| src/DevLeads.Core/Ai/IAiTriageProvider.cs | 56 | property | public string Model { get; } |
| src/DevLeads.Core/Ai/IAiTriageProvider.cs | 57 | property | public string RequestJson { get; } |
| src/DevLeads.Core/Ai/IAiTriageProvider.cs | 58 | property | public string? ResponseJson { get; } |
| src/DevLeads.Core/Ai/IAiTriageProvider.cs | 59 | property | public string? ErrorMessage { get; } |
| src/DevLeads.Core/Ai/IAiTriageProvider.cs | 60 | property | public bool Retryable { get; } |
| src/DevLeads.Core/Ai/IAiTriageProvider.cs | 62 | interface | public interface IAiBatchShortlistProvider |
| src/DevLeads.Core/Ai/IAiTriageProvider.cs | 71 | interface | public interface IAiTriageProvider |
| src/DevLeads.Core/AiTriageResult.cs | 4 | class | public sealed class AiTriageResult |
| src/DevLeads.Core/AiTriageResult.cs | 12 | property | public bool IsTechnicalProblem { get; } |
| src/DevLeads.Core/AiTriageResult.cs | 14 | property | public bool IsEmergency { get; } |
| src/DevLeads.Core/AiTriageResult.cs | 17 | property | public string PaymentIntent { get; } |
| src/DevLeads.Core/AiTriageResult.cs | 25 | property | public bool AssistanceRequested { get; } |
| src/DevLeads.Core/AiTriageResult.cs | 32 | property | public string? RejectReason { get; } |
| src/DevLeads.Core/AiTriageResult.cs | 35 | property | public string ProblemCategory { get; } |
| src/DevLeads.Core/AiTriageResult.cs | 38 | property | public List<string> DetectedStack { get; } |
| src/DevLeads.Core/AiTriageResult.cs | 41 | property | public string EstimatedCause { get; } |
| src/DevLeads.Core/AiTriageResult.cs | 44 | property | public string FirstDiagnosticStep { get; } |
| src/DevLeads.Core/AiTriageResult.cs | 47 | property | public int? EstimatedFixMinutesMin { get; } |
| src/DevLeads.Core/AiTriageResult.cs | 50 | property | public int? EstimatedFixMinutesMax { get; } |
| src/DevLeads.Core/AiTriageResult.cs | 53 | property | public decimal AiConfidence { get; } |
| src/DevLeads.Core/AiTriageResult.cs | 56 | property | public string OutreachRecommendation { get; } |
| src/DevLeads.Core/Connectors/ISourceConnector.cs | 4 | class | public sealed class SourceConnectorConfig |
| src/DevLeads.Core/Connectors/ISourceConnector.cs | 8 | property | public string SourceKey { get; } |
| src/DevLeads.Core/Connectors/ISourceConnector.cs | 9 | property | public int MaxItems { get; } |
| src/DevLeads.Core/Connectors/ISourceConnector.cs | 10 | property | public IReadOnlyList<string> Terms { get; } |
| src/DevLeads.Core/Connectors/ISourceConnector.cs | 12 | property | public DateTimeOffset? Since { get; } |
| src/DevLeads.Core/Connectors/ISourceConnector.cs | 13 | property | public IReadOnlyList<string> SkillTerms { get; } |
| src/DevLeads.Core/Connectors/ISourceConnector.cs | 17 | class | public sealed class ConnectorHealth |
| src/DevLeads.Core/Connectors/ISourceConnector.cs | 21 | property | public bool Healthy { get; } |
| src/DevLeads.Core/Connectors/ISourceConnector.cs | 22 | property | public string Message { get; } |
| src/DevLeads.Core/Connectors/ISourceConnector.cs | 23 | property | public int ItemCount { get; } |
| src/DevLeads.Core/Connectors/ISourceConnector.cs | 24 | property | public DateTimeOffset CheckedAt { get; } |
| src/DevLeads.Core/Connectors/ISourceConnector.cs | 26 | interface | public interface ISourceConnector |
| src/DevLeads.Core/Entities/AiTriageRun.cs | 2 | class | public class AiTriageRun |
| src/DevLeads.Core/Entities/AiTriageRun.cs | 6 | property | public long Id { get; } |
| src/DevLeads.Core/Entities/AiTriageRun.cs | 7 | property | public long OpportunityId { get; } |
| src/DevLeads.Core/Entities/AiTriageRun.cs | 8 | property | public string Provider { get; } |
| src/DevLeads.Core/Entities/AiTriageRun.cs | 10 | property | public string Model { get; } |
| src/DevLeads.Core/Entities/AiTriageRun.cs | 11 | property | public string PromptVersion { get; } |
| src/DevLeads.Core/Entities/AiTriageRun.cs | 12 | property | public AiJobStatus Status { get; } |
| src/DevLeads.Core/Entities/AiTriageRun.cs | 13 | property | public string RequestJson { get; } |
| src/DevLeads.Core/Entities/AiTriageRun.cs | 15 | property | public string? ResponseJson { get; } |
| src/DevLeads.Core/Entities/AiTriageRun.cs | 16 | property | public string? ErrorMessage { get; } |
| src/DevLeads.Core/Entities/AiTriageRun.cs | 17 | property | public DateTimeOffset StartedAt { get; } |
| src/DevLeads.Core/Entities/AiTriageRun.cs | 19 | property | public DateTimeOffset? CompletedAt { get; } |
| src/DevLeads.Core/Entities/AiTriageRun.cs | 20 | property | public Opportunity? Opportunity { get; } |
| src/DevLeads.Core/Entities/AuditEvent.cs | 2 | class | public class AuditEvent |
| src/DevLeads.Core/Entities/AuditEvent.cs | 6 | property | public long Id { get; } |
| src/DevLeads.Core/Entities/AuditEvent.cs | 7 | property | public string EntityType { get; } |
| src/DevLeads.Core/Entities/AuditEvent.cs | 8 | property | public long EntityId { get; } |
| src/DevLeads.Core/Entities/AuditEvent.cs | 9 | property | public string EventType { get; } |
| src/DevLeads.Core/Entities/AuditEvent.cs | 10 | property | public string Actor { get; } |
| src/DevLeads.Core/Entities/AuditEvent.cs | 11 | property | public string Description { get; } |
| src/DevLeads.Core/Entities/AuditEvent.cs | 12 | property | public string MetadataJson { get; } |
| src/DevLeads.Core/Entities/AuditEvent.cs | 13 | property | public DateTimeOffset CreatedAt { get; } |
| src/DevLeads.Core/Entities/OperatorSettings.cs | 2 | class | public class OperatorSettings |
| src/DevLeads.Core/Entities/OperatorSettings.cs | 6 | property | public long Id { get; } |
| src/DevLeads.Core/Entities/OperatorSettings.cs | 7 | property | public string OperatorName { get; } |
| src/DevLeads.Core/Entities/OperatorSettings.cs | 10 | property | public string BusinessName { get; } |
| src/DevLeads.Core/Entities/OperatorSettings.cs | 11 | property | public string Location { get; } |
| src/DevLeads.Core/Entities/OperatorSettings.cs | 12 | property | public string RemoteAvailability { get; } |
| src/DevLeads.Core/Entities/OperatorSettings.cs | 13 | property | public string CoreSkills { get; } |
| src/DevLeads.Core/Entities/OperatorSettings.cs | 14 | property | public string SecondarySkills { get; } |
| src/DevLeads.Core/Entities/OperatorSettings.cs | 15 | property | public double MinimumFee { get; } |
| src/DevLeads.Core/Entities/OperatorSettings.cs | 16 | property | public string PreferredPaymentTerms { get; } |
| src/DevLeads.Core/Entities/OperatorSettings.cs | 17 | property | public bool EmergencyAvailability { get; } |
| src/DevLeads.Core/Entities/OperatorSettings.cs | 18 | property | public string AiProvider { get; } |
| src/DevLeads.Core/Entities/OperatorSettings.cs | 22 | property | public string AiModel { get; } |
| src/DevLeads.Core/Entities/OperatorSettings.cs | 25 | property | public string OpenCodeCliPath { get; } |
| src/DevLeads.Core/Entities/OperatorSettings.cs | 32 | property | public string PromptVersion { get; } |
| src/DevLeads.Core/Entities/OperatorSettings.cs | 33 | property | public int MaxAiCallsPerHour { get; } |
| src/DevLeads.Core/Entities/OperatorSettings.cs | 34 | property | public double MaxAiSpendPerDay { get; } |
| src/DevLeads.Core/Entities/OperatorSettings.cs | 35 | property | public double MinPreFilterScoreForAi { get; } |
| src/DevLeads.Core/Entities/OperatorSettings.cs | 36 | property | public double MinAiConfidenceForDraft { get; } |
| src/DevLeads.Core/Entities/OperatorSettings.cs | 37 | property | public double ManualReviewConfidenceThreshold { get; } |
| src/DevLeads.Core/Entities/OperatorSettings.cs | 38 | property | public int AiRetryCount { get; } |
| src/DevLeads.Core/Entities/OperatorSettings.cs | 39 | property | public int AiTimeoutSeconds { get; } |
| src/DevLeads.Core/Entities/OperatorSettings.cs | 40 | property | public OutreachMode DefaultOutreachMode { get; } |
| src/DevLeads.Core/Entities/OperatorSettings.cs | 43 | property | public bool GlobalAutoModeEnabled { get; } |
| src/DevLeads.Core/Entities/OperatorSettings.cs | 44 | property | public bool GlobalKillSwitch { get; } |
| src/DevLeads.Core/Entities/OperatorSettings.cs | 45 | property | public int MaxSendsPerHour { get; } |
| src/DevLeads.Core/Entities/OperatorSettings.cs | 46 | property | public int MaxSendsPerDay { get; } |
| src/DevLeads.Core/Entities/OperatorSettings.cs | 47 | property | public double RequireApprovalAboveRisk { get; } |
| src/DevLeads.Core/Entities/OperatorSettings.cs | 48 | property | public double RequireApprovalBelowConfidence { get; } |
| src/DevLeads.Core/Entities/OperatorSettings.cs | 49 | property | public bool SuppressionListEnabled { get; } |
| src/DevLeads.Core/Entities/OperatorSettings.cs | 50 | property | public bool AuditLoggingEnabled { get; } |
| src/DevLeads.Core/Entities/OperatorSettings.cs | 51 | property | public double DraftScoreThreshold { get; } |
| src/DevLeads.Core/Entities/OperatorSettings.cs | 54 | property | public double AlertScoreThreshold { get; } |
| src/DevLeads.Core/Entities/OperatorSettings.cs | 55 | property | public bool DiscoveryEnabled { get; } |
| src/DevLeads.Core/Entities/OperatorSettings.cs | 58 | property | public int StaleItemMaxAgeHours { get; } |
| src/DevLeads.Core/Entities/OperatorSettings.cs | 59 | property | public int FollowUpDefaultHours { get; } |
| src/DevLeads.Core/Entities/Opportunity.cs | 2 | class | public class Opportunity |
| src/DevLeads.Core/Entities/Opportunity.cs | 8 | property | public long Id { get; } |
| src/DevLeads.Core/Entities/Opportunity.cs | 9 | property | public string Title { get; } |
| src/DevLeads.Core/Entities/Opportunity.cs | 11 | property | public string Summary { get; } |
| src/DevLeads.Core/Entities/Opportunity.cs | 12 | property | public string SourceKey { get; } |
| src/DevLeads.Core/Entities/Opportunity.cs | 13 | property | public string SourceUrl { get; } |
| src/DevLeads.Core/Entities/Opportunity.cs | 14 | property | public string? AuthorName { get; } |
| src/DevLeads.Core/Entities/Opportunity.cs | 15 | property | public string? AuthorProfileUrl { get; } |
| src/DevLeads.Core/Entities/Opportunity.cs | 16 | property | public OpportunityStatus Status { get; } |
| src/DevLeads.Core/Entities/Opportunity.cs | 18 | property | public Priority Priority { get; } |
| src/DevLeads.Core/Entities/Opportunity.cs | 19 | property | public double Score { get; } |
| src/DevLeads.Core/Entities/Opportunity.cs | 22 | property | public double UrgencyScore { get; } |
| src/DevLeads.Core/Entities/Opportunity.cs | 23 | property | public double StackFitScore { get; } |
| src/DevLeads.Core/Entities/Opportunity.cs | 24 | property | public double BusinessValueScore { get; } |
| src/DevLeads.Core/Entities/Opportunity.cs | 25 | property | public double ReachabilityScore { get; } |
| src/DevLeads.Core/Entities/Opportunity.cs | 26 | property | public double CompetitionScore { get; } |
| src/DevLeads.Core/Entities/Opportunity.cs | 27 | property | public double TrustScore { get; } |
| src/DevLeads.Core/Entities/Opportunity.cs | 28 | property | public string ProblemType { get; } |
| src/DevLeads.Core/Entities/Opportunity.cs | 31 | property | public string PaymentIntent { get; } |
| src/DevLeads.Core/Entities/Opportunity.cs | 33 | property | public bool? AssistanceRequested { get; } |
| src/DevLeads.Core/Entities/Opportunity.cs | 36 | property | public string DetectedStackJson { get; } |
| src/DevLeads.Core/Entities/Opportunity.cs | 37 | property | public string SuggestedFirstStep { get; } |
| src/DevLeads.Core/Entities/Opportunity.cs | 38 | property | public string EstimatedCause { get; } |
| src/DevLeads.Core/Entities/Opportunity.cs | 39 | property | public double? EstimatedFeeMin { get; } |
| src/DevLeads.Core/Entities/Opportunity.cs | 41 | property | public double? EstimatedFeeMax { get; } |
| src/DevLeads.Core/Entities/Opportunity.cs | 42 | property | public bool FeeIsEstimate { get; } |
| src/DevLeads.Core/Entities/Opportunity.cs | 48 | property | public int? EstimatedFixMinutesMin { get; } |
| src/DevLeads.Core/Entities/Opportunity.cs | 49 | property | public int? EstimatedFixMinutesMax { get; } |
| src/DevLeads.Core/Entities/Opportunity.cs | 50 | property | public double AiConfidence { get; } |
| src/DevLeads.Core/Entities/Opportunity.cs | 52 | property | public OutreachRecommendation OutreachRecommendation { get; } |
| src/DevLeads.Core/Entities/Opportunity.cs | 53 | property | public string? RejectionReason { get; } |
| src/DevLeads.Core/Entities/Opportunity.cs | 54 | property | public AiJobStatus AiJobStatus { get; } |
| src/DevLeads.Core/Entities/Opportunity.cs | 56 | property | public double HeuristicScore { get; } |
| src/DevLeads.Core/Entities/Opportunity.cs | 59 | property | public string MatchedTermsJson { get; } |
| src/DevLeads.Core/Entities/Opportunity.cs | 60 | property | public string? PreFilterRejectReason { get; } |
| src/DevLeads.Core/Entities/Opportunity.cs | 61 | property | public bool AutoEligible { get; } |
| src/DevLeads.Core/Entities/Opportunity.cs | 64 | property | public DateTimeOffset PostedAt { get; } |
| src/DevLeads.Core/Entities/Opportunity.cs | 66 | property | public DateTimeOffset FirstSeenAt { get; } |
| src/DevLeads.Core/Entities/Opportunity.cs | 67 | property | public DateTimeOffset LastSeenAt { get; } |
| src/DevLeads.Core/Entities/Opportunity.cs | 68 | property | public DateTimeOffset CreatedAt { get; } |
| src/DevLeads.Core/Entities/Opportunity.cs | 69 | property | public DateTimeOffset UpdatedAt { get; } |
| src/DevLeads.Core/Entities/Opportunity.cs | 70 | property | public DateTimeOffset? NextFollowUpAt { get; } |
| src/DevLeads.Core/Entities/Opportunity.cs | 73 | property | public string? WorkNotes { get; } |
| src/DevLeads.Core/Entities/Opportunity.cs | 74 | property | public List<AiTriageRun> TriageRuns { get; } |
| src/DevLeads.Core/Entities/Opportunity.cs | 77 | property | public List<OutreachAttempt> OutreachAttempts { get; } |
| src/DevLeads.Core/Entities/Opportunity.cs | 78 | property | public List<Quote> Quotes { get; } |
| src/DevLeads.Core/Entities/Opportunity.cs | 79 | property | public List<WorkSession> WorkSessions { get; } |
| src/DevLeads.Core/Entities/OutreachAttempt.cs | 2 | class | public class OutreachAttempt |
| src/DevLeads.Core/Entities/OutreachAttempt.cs
...[truncated]


---

# Test Map

Test names are often the fastest behavior summary.

_None found._

---

# Token Estimate

Approximation: 1 token ≈ 4 characters.

| Context | Characters | Estimated tokens |
| --- | ---: | ---: |
| Full indexed files | 371,691 | 92,923 |
| AI_CONTEXT_COMPACT.md | 29,432 | 7,358 |

Estimated context reduction: **92.1%**

This is approximate, but good enough to show whether the script is doing useful work.
