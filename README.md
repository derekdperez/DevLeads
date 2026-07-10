# DevLeads

**A solo-operator command center that finds, triages, prioritizes, and helps respond to urgent software development leads posted publicly online.**

DevLeads discovers time-sensitive software development opportunities (urgent issues, contract gigs, technical pain points), rejects noise with a **zero-cost heuristic pre-filter**, sends promising items through structured triage, scores each opportunity, and drafts specific responses that require **human approval by default**. It then tracks the full path from lead → quote → job completion → payment.

> Core loop: **Discover · pre-filter cheaply · analyze once · score fit & value · draft specific responses · require human approval · track quote/fix/payment.**

---

## Architecture

```
Heuristic Pre-Filter  →  Structured Triage  →  Weighted Scoring  →  HITL Outreach
     (zero cost)          (AI or heuristic)      (6 components)     (approval queue)
```

| Project | Responsibility |
|---|---|
| **DevLeads.Core** | Domain models, workflow enums, query packs, heuristic pre-filter, weighted scorer, red-flag detector, AI abstractions, templates |
| **DevLeads.Infrastructure** | EF Core (SQLite) + seeding, source connectors, AI providers, outreach/quote/audit services, background discovery worker |
| **DevLeads.Web** | Blazor Web App (interactive server) dashboard + internal JSON API |

### Pipeline (per discovered item)
1. Connector fetches recent public items → normalized `RawSourceItem`.
2. Duplicate detector skips items already seen (`SourceKey` + `ExternalId`).
3. **Heuristic pre-filter** rejects obvious noise before any AI cost.
4. Survivors get structured triage returning strict JSON (relevance, emergency, category, stack, cause, first step, fix time, confidence, recommendation).
5. **Weighted scorer** blends urgency, stack-fit, business value, reachability, competition, and trust into a 0–100 score + priority band.
6. High-score, high-confidence, legitimate leads get a **draft** that enters the approval queue.

---

## AI providers

The AI backend is pluggable (`IAiTriageProvider`):

- **OpenCode CLI** — the default provider. DevLeads shells out to `opencode run --model nvidia/minimaxai/minimax-m3 ...`, so model/provider auth stays in OpenCode rather than in this app.
- **Anthropic (Claude)** — official Anthropic SDK, single structured-output call (`claude-opus-4-8`). Enabled when `ANTHROPIC_API_KEY` is set and the provider is selected in Settings.
- **Heuristic (offline)** — a zero-cost, no-network rules engine that produces a structured result. High-volume community sources can use this as their first-pass triage provider, with manual rerun available for interesting leads.

Install and authenticate OpenCode to use the default NVIDIA MiniMax M3 model:

```bash
curl -fsSL https://opencode.ai/install | bash
opencode auth login
```

In `opencode auth login`, choose NVIDIA and enter an NVIDIA API key. You can verify the available model id with:

```bash
opencode models nvidia --refresh
```

Set an API key and switch the provider on the **Settings** page to use Claude instead:

```bash
export ANTHROPIC_API_KEY=sk-ant-...
```

---

## Running

Requires the **.NET 10 SDK**.

```bash
dotnet run --project src/DevLeads.Web
```

The SQLite database is created and seeded automatically on first run (query packs, source configs, and settings) under `src/DevLeads.Web/App_Data/devleads.db`.

Open the URL printed in the console (e.g. `http://localhost:5167`).

### Optional environment
- `NVIDIA_API_KEY` — enables OpenCode's NVIDIA provider when supplied through the environment instead of `opencode auth login`.
- `ANTHROPIC_API_KEY` — enables the Claude triage provider.

---

## Features

- **Dashboard** — critical/high counts, drafts pending, AI review needed, contacted/replies, quotes, work in progress, follow-ups due, and a top-opportunities table.
- **Opportunities grid** — filter by lead quality, freshness, source, category, status, score floor, and stack/payment fit; search across title, summary, stack, author, source, source URL, and matched terms.
- **Opportunity detail** — obvious original-source links, original post, full triage result, score breakdown bars, matched pre-filter terms, response drafts with approve/send/copy, quote & payment panel, work session with emergency checklist, and full audit history.
- **Approval queue** — review, approve, record-sent, or cancel drafts. Nothing is sent automatically in HITL mode.
- **Quotes & payment** — generate flat-fee quotes, send, and track paid/overdue.
- **Sources** — enable/disable connectors, edit poll intervals/query packs/thresholds/parameters, run one source, run all enabled sources, and test health.
- **Settings** — operator profile, AI provider/model/thresholds, outreach safety (default mode, suppression, audit logging, **global kill switch**), and discovery/scoring thresholds.

## Safety controls
Human-in-the-loop approval by default · global kill switch for all outbound messages · suppression list · red-flag detector (unauthorized access, credential theft, malware, fraud → *Do Not Contact*) · audit log for every generated/sent message · connectors are read-only and never send.

---

## Internal API (selected)

```
GET  /api/opportunities            POST /api/opportunities/manual
GET  /api/opportunities/{id}       POST /api/opportunities/{id}/{approve|reject|watch|mark-contacted|mark-won|mark-lost}
POST /api/opportunities/{id}/rerun-triage
POST /api/opportunities/{id}/draft-response   POST /api/outreach/{id}/{approve|send|cancel}
POST /api/opportunities/{id}/generate-quote   POST /api/quotes/{id}/{send|mark-paid|mark-overdue}
GET  /api/sources                  POST /api/sources/run-all
POST /api/sources/{key}/{test|run-now}
```

## Sources
Sources are now community-first: regular support and operator communities are prioritized over generic job boards, because they surface urgent business pain instead of generic hiring funnels.

| Source | Why it pays |
|---|---|
| **Reddit pain — WordPress + Shopify** (r/wordpress, r/woocommerce, r/shopify, r/ecommerce) | Regular community posts surface urgent store/site failures, plugin conflicts, checkout issues, and merchant pain |
| **Reddit pain — webdev + ops** (r/webdev, r/sysadmin, r/devops, r/webhosting, r/dotnet) | Real production, hosting, deployment, and framework problems from operators and developers |
| **Reddit pain — business + ecommerce** (r/smallbusiness, r/Entrepreneur, r/ecommerce, r/SaaS) | Business owners describe revenue-impacting website, checkout, automation, and SaaS problems |
| **WordPress.org support** (troubleshooting + selected plugin feeds) | Surfaces live WordPress failures; lower pay-intent, but useful when posts mention business impact or urgent breakage |
| **Shopify Developer Community** (latest RSS) | Shopify API, checkout, app, webhook, and storefront issues from merchants/developers |
| **Hacker News** (Algolia search) | Founders/operators posting real production pain |
| **Stack Exchange** (Server Fault, Stack Overflow, DBA, Webmasters, WordPress, Drupal, Magento, Ask Ubuntu, Unix, Super User) | Real incidents, but askers usually want free answers — enabled only for manual-review radar |
| **Job board feeds** (Remotive, WeWorkRemotely, RemoteOK, Jobicy, Remote First Jobs, Real Work From Anywhere) | Available but disabled by default; useful for paid contract work, not the primary discovery path |
| **Reddit hiring subs** (r/forhire, r/jobbit, r/hireawebdeveloper, r/hireaprogrammer, …) | Available but disabled by default; `[Hiring]`/`[Task]`/`[Paid]` posts are explicit budget signals |

GitHub Issues was removed: open-source bug reports are volunteer work with no budget attached. Connectors use targeted query packs, freshness windows, and de-duplication before the pre-filter/triage pipeline; all are resilient to network failure and report health. Slack/Discord are intentionally out of scope (bot use requires per-community permission).

### Lead quality guarantees
- **No simulated or demo data anywhere** — every lead comes from a live fetch or manual entry; triage never invents a stack or category the text doesn't support.
- Automated discovery **only stores payable leads**: pre-filter rejects, irrelevant posts, and red-flagged posts never become lead rows (their raw items are kept solely for de-duplication).
- Posts with no software context (e.g. news matching "production down") are classified *Not Relevant* and dropped.
- High-volume community sources use fast heuristic triage first, so they can create a regular review queue without tying every item to a slow external model call.
- Startup migration purges legacy non-actionable leads; operator-engaged leads (contacted, quoted, in progress, paid) are never auto-purged.

## Notes for maintainers
- SQLite can't order/compare `DateTimeOffset` (stored as TEXT), so the `DbContext` converts every `DateTimeOffset` to sortable UTC ticks.
- The internal API returns EF entities with cycle-safe JSON and string enums, and disables browser antiforgery (the Blazor UI calls services directly, not the HTTP API).
