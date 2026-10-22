# Pinta — guidance for Claude Code

Pinta is a mobile-first PWA for booking interior and exterior painting in the Lisbon district (Portugal). A client builds a quote with a simple visual schema of their home, a human admin reviews and confirms the price, the client pays a 30% deposit (card, MB WAY or Multibanco via Stripe), and a curated painter does the job. The software is owned by NHIT-DEV. Current goal: a complete MVP running **locally** for a demo to the business partner.

The full PRD lives in `docs/` (written in Spanish). Read the files a task names before writing code. If code and docs disagree, the docs win until they are updated. Never change a product rule silently: update the doc first, then the code.

| File | What it defines |
|---|---|
| `docs/prd-overview.md` | Demo goal, roles and permissions, scope, non-functional requirements, risks |
| `docs/functional-requirements.md` | User stories with acceptance criteria (IDs C*, A*, P*, S*) |
| `docs/pricing-engine.md` + `docs/reference/pricing_reference.py` | Exact pricing algorithm and golden test cases TC01–TC13 |
| `docs/screens.md` | Every screen: route, data, actions, validations, empty/loading/error states |
| `docs/api.md` | HTTP contract, examples, enums, error codes |
| `docs/states.md` | Job state machine — the only allowed transitions |
| `docs/data-model.md` | Tables per module (Spanish names; see glossary for code names) |
| `docs/glossary.md` | One name per concept: Spanish doc term ↔ code ↔ PT/EN UI; table names |
| `docs/architecture.md`, `docs/structure.md`, `docs/stack.md`, `docs/integrations.md` | Architecture, repo layout, stack, Stripe/S3/email/auth details |
| `docs/plan.md` | Milestones H0–H8 with exit criteria, definition of done, demo seed, demo script |

Visual reference for screens: design canvas at https://claude.ai/artifact/QXmJ1wZgXMpmRUHx6pHXKz (board names are given in `docs/screens.md`).

## Stack

- Backend: ASP.NET Core on .NET 10 LTS, Minimal APIs, vertical slices, EF Core + Npgsql, PostgreSQL, ASP.NET Core Identity (cookie auth, own accounts + Google), Hangfire on PostgreSQL, Serilog, health checks.
- Frontend: one Next.js app (React + TypeScript, App Router) as a PWA; Tailwind + shadcn/ui; TanStack Query; React Hook Form + Zod; next-intl with `/pt` (default), `/es`, `/en`; typed API client generated from the API's OpenAPI.
- Local orchestration: .NET Aspire AppHost runs PostgreSQL, MinIO (S3), Mailpit, Stripe CLI, the API and the web app.
- Tests: xUnit, Testcontainers (real PostgreSQL), architecture tests (NetArchTest or equivalent), frontend unit tests with Vitest.

## Architecture rules (enforced by `tests/Pinta.ArchitectureTests`)

1. Modular monolith. Modules: Identity, Catalog, Quoting, Jobs, Painters, Payments, Media, Notifications, Finance, plus the pure library `Pinta.Pricing` and `Pinta.SharedKernel`.
2. Each module = `Pinta.Modules.X.Contracts` (public interfaces, DTOs, integration events) + `Pinta.Modules.X` (private: Domain, Data, Features).
3. A module may reference only other modules' `.Contracts`, `Pinta.SharedKernel` and `Pinta.Pricing`. Never another module's internals.
4. One PostgreSQL schema and one DbContext per module. No foreign keys across schemas: reference by ID, integrity in code.
5. Side effects across modules go through in-process domain events; slow or failure-prone work (emails, Stripe calls, expirations) runs in Hangfire with retries.
6. The `Job` state machine lives in the Jobs domain and implements exactly `docs/states.md`. Any other transition returns `409 invalid_transition`.
7. `Pinta.Pricing` has no dependencies, no I/O and no clock. It takes the frozen price snapshot plus elements and returns an immutable breakdown.
8. Responses are shaped per role on the server. A painter never receives client price, material breakdown or margin; a client never receives rates, painter pay or margin.

## Conventions

- All code, identifiers, comments, commits and DB names in **English**. UI text only via translation files (`web/messages/{pt,es,en}.json`), never hard-coded.
- Names come from `docs/glossary.md` (e.g. `Job`, `QuoteVersion`, `Deposit`, `PainterPayout`). Do not invent synonyms.
- Money: `decimal` in C#, `numeric(12,2)` in PostgreSQL, **integer cents** in the API (`...Cents`). Never `double`/`float`. Round each line to 2 decimals with `MidpointRounding.AwayFromZero`; totals are sums of rounded lines.
- Time: store `timestamptz` in UTC; display and schedule in `Europe/Lisbon`. Inject a clock (`TimeProvider`) — never call `DateTime.Now` in domain code.
- IDs: UUID v7. Public job code `PT-YYYY-NNNN` is a separate field.
- Translatable catalog text: `jsonb` `{ "pt": "…", "es": "…", "en": "…" }`.
- API: JSON camelCase, enums as `snake_case` strings, errors as ProblemDetails (RFC 9457) with a stable `code` from `docs/api.md`, pagination `{ items, page, pageSize, total }`, `Idempotency-Key` on job creation and payments, `X-XSRF-TOKEN` antiforgery header on mutations.
- Optimistic concurrency token on `Job` and catalog entities → `409 concurrency_conflict`.
- Sensitive data: painter IBAN encrypted with ASP.NET Core Data Protection and always masked in responses; no personal data or IBAN in logs.
- Secrets (Stripe test keys, Google OAuth) only in `dotnet user-secrets`. Never commit secrets.
- EF Core: one migration per change with a descriptive name; never edit a merged migration.

## Pricing engine

The golden cases TC01–TC13 in `docs/pricing-engine.md` are parameterized tests in `tests/Pinta.Pricing.Tests` and must match to the cent. `docs/reference/pricing_reference.py` produced them (`python3 docs/reference/pricing_reference.py`). To change a pricing rule: change the reference script, regenerate the numbers, update the doc, then the .NET code. Do not touch `Pinta.Pricing` without all golden tests green.

## Running and testing

```bash
dotnet run --project src/Pinta.AppHost      # starts everything; Aspire dashboard shows URLs, logs, traces
dotnet test                                 # all .NET tests (Docker must be running for Testcontainers)
cd web && pnpm install && pnpm dev          # frontend alone, if needed
cd web && pnpm lint && pnpm typecheck && pnpm test
cd web && pnpm api:generate                 # regenerate the typed client after API changes
```

Local tools: Mailpit UI for emails, MinIO console for files, `/hangfire` (admin only) for jobs, Stripe CLI forwards webhooks to `/api/webhooks/stripe`. Demo data is seeded in Development on an empty DB; Admin → Ajustes → "Restablecer datos de demo" resets it with dates relative to today.

## How we work

- One milestone (H0–H8 in `docs/plan.md`) per branch (`h1-pricing`, `h2-constructor`, …). Propose a plan first and wait for review before writing code.
- Small tasks, each with tests and its own commit. A task is done only when it meets the definition of done in `docs/plan.md` (tests green, OpenAPI client regenerated, three languages, 390 px and 1440 px, empty/loading/error states, role and ownership checks).
- Reference user stories by ID in commits and test names (e.g. `C2.3`).
- Every push updates `CHANGELOG.md` (in Spanish, Keep a Changelog sections) under **Sin publicar**, in the current milestone's block: what changed for users or developers, with story IDs. When a milestone merges into `main`, its block gets the date and the PR link.
- When something in the PRD is ambiguous or missing, ask instead of guessing; if you must proceed, choose the simplest option and note it for the doc.
