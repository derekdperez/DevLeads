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
