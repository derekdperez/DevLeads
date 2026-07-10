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
