# DevLeads (UrgentLeads)

<!-- ai-hacks:instructions:begin -->
## Mandatory AI project bootstrap

Before analyzing, planning, editing, or running broad searches in this repository:

1. Read `.ai_hacks/AI_CONTEXT_COMPACT.md` completely.
2. Treat its **How to use this map**, **Change routing**, and **Critical invariants** sections as mandatory project instructions, unless they conflict with a higher-priority user or system instruction.
3. Use its type/method index, routes, DI, and persistence maps to narrow the task before opening implementation files.
4. If the generated context is missing or stale, run `python3 ai_hacks.py` from the repository root, then confirm it with `python3 ai_hacks.py --check`.
5. After structural changes, regenerate the context and include the resulting `.ai_hacks/` updates with the code change.

Do not edit this managed block by hand; edit `ai_hacks.py` or `ai_hacks.context.json` and regenerate it.
<!-- ai-hacks:instructions:end -->

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
- Lead quality bar: explicit paid work ranks first; first-person business owners with a
  concrete hands-on need may also qualify as lower-ranked networking leads. Topic keywords
  ("payments", "budget" as ad spend, product pricing copy) and generic advice requests do not.
- Technology fit: the operator is strongest in .NET but is a senior generalist who can
  deliver across web, backend, cloud, mobile, and other stacks. Stack familiarity is only
  a light ranking preference; paid-work quality, professional compensation, remote
  feasibility, competition, and poster reliability drive lead selection.
- Freshness: automated discovery ignores posts older than 30 days before AI triage.
  Preserve manual and operator-engaged rows, but never show expired posts as dashboard leads.
- Near misses: candidates that pass the hard quality gates but reach only 50–99% of a
  source's required score are retained as `JustMissed` for dashboard scoring review; they
  stay out of normal active-lead and automatic-outreach paths.
