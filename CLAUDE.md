# DevLeads (UrgentLeads)

**Start every session by reading `.ai_hacks/AI_CONTEXT_COMPACT.md`.** It is a generated
project map (files, entry points, DI registrations, public API surface) that lets you
navigate the solution without opening implementation files until you have narrowed the
task to a specific file, symbol, route, or registration.

Deeper generated references live alongside it in `.ai_hacks/` (PUBLIC_API.md,
ROUTES_AND_DI.md, ENTRYPOINTS.md, DEPENDENCIES.md). Regenerate them after significant
structural changes with `python3 ai_hacks.py` from the repo root.

## Quick facts

- .NET 10 Blazor Server app; solution file `DevLeads.slnx`, projects under `src/`
  (Core → Infrastructure → Web).
- Dev instance runs at `http://localhost:5167` (SQLite DB at
  `src/DevLeads.Web/App_Data/devleads.db`). Killing the process triggers an in-app
  restart service; it relaunches from `bin/Debug/net10.0`, so `dotnet build` first.
- Lead pipeline: connectors → `HeuristicPreFilter` (scoped to the source's QueryPacksCsv)
  → AI shortlist/triage (`AiTriageRouter`: OpenCode CLI → Anthropic → Heuristic fallback)
  → `OpportunityScorer` → status/draft decisions in `LeadIngestionService`.
- Campaigns: every source and lead belongs to a `Campaign` (seeded: `emergency`,
  `dotnet_modernization`); the campaign `Objective` is injected into the AI shortlist and
  triage prompts, and the UI is scoped by the sidebar switcher (persisted in
  `OperatorSettings.SelectedCampaignId`, null = all campaigns).
- `DatabaseSeeder` re-applies seeded source defaults on boot; changing seeded
  `ParametersJson` counts as a migration and purges + re-ingests discovery leads
  (purely additive new sources do NOT purge).
- Lead quality bar: pay-intent signals must be first-person ownership or hire language,
  never topic keywords ("payments", "budget" as ad spend, product pricing copy).
- Stack gate: the operator is a pure .NET consultant. Leads must match a weight-3
  "Primary stack" identity skill (C#/.NET/SQL Server/Azure…) or they cap at 50; posts
  demanding a foreign stack (Go/Python/Java…) without one are rejected. Never seed
  capability phrases ("REST API") into the "Primary stack" category.
