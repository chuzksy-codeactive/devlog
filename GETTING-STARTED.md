# DevLog — Getting Started

What's already been built, and exactly what to run in Visual Studio 2026 / a terminal to get it working. This sandbox has no .NET SDK, Docker, or NuGet access, so the C# code below is hand-authored and correct to the best of my knowledge, but **not compiled or run** — treat "first build" as the real first test of it. The Next.js scaffold, by contrast, was actually generated and its files verified.

---

## What's here

```
Devlog.os/
├── docker-compose.yml          — local Postgres (pgvector image pre-installed)
├── api/
│   ├── DevLog.Domain/          — entities + enums, no framework dependencies
│   ├── DevLog.Infrastructure/  — EF Core DbContext, RLS connection interceptor
│   ├── DevLog.Api/             — ASP.NET Core Web API, Clerk JWT auth
│   ├── DevLog.Tests/           — the tenant-isolation proof test
│   └── sql/001_row_level_security.sql — RLS policies (EF Core has no native RLS API)
└── web/                        — Next.js app (TypeScript, App Router, Tailwind), unmodified create-next-app scaffold
```

This covers the schema/auth/isolation portion of Sprint 0. Not yet done: the `.sln` file itself (see Step 2 — safer for you to generate it than for me to hand-type project GUIDs I can't verify), and everything from "Sprint Journal entry creation" onward per the MVP roadmap.

---

## Step 1 — start local Postgres

```bash
cd Devlog.os
docker compose up -d
```

## Step 2 — create the solution file

I deliberately didn't hand-write `DevLog.sln` — solution files are GUID-heavy and easy to get subtly wrong without the tooling to verify them. Generate it for real instead:

```bash
cd Devlog.os/api
dotnet new sln -n DevLog
dotnet sln add DevLog.Domain/DevLog.Domain.csproj
dotnet sln add DevLog.Infrastructure/DevLog.Infrastructure.csproj
dotnet sln add DevLog.Api/DevLog.Api.csproj
dotnet sln add DevLog.Tests/DevLog.Tests.csproj
```

Or just open the `api` folder in Visual Studio 2026 and use "Add existing project" for each `.csproj` into a new blank solution — whichever is faster for you.

## Step 3 — restore and build

```bash
dotnet restore
dotnet build
```

If anything doesn't compile, that's genuinely useful signal — it's the first real check this code has had. Most likely candidates if something's off: package version numbers in the `.csproj` files (I used 8.0.10 for EF Core/Npgsql packages; bump if NuGet has moved past that by the time you restore) or the `HasConversion`/`ValueComparer` syntax in `DevLogDbContext.cs`, which is the most intricate piece.

## Step 4 — create and apply the first migration

```bash
cd DevLog.Api
dotnet ef migrations add InitialSchema --project ../DevLog.Infrastructure --startup-project .
dotnet ef database update --project ../DevLog.Infrastructure --startup-project .
```

(Install the tool first if needed: `dotnet tool install --global dotnet-ef`.)

## Step 5 — apply Row-Level Security

EF Core migrations can't express RLS natively, so this stays a plain SQL script:

```bash
psql -h localhost -U devlog -d devlog -f ../sql/001_row_level_security.sql
```
(Password: `devlog_local`, from `docker-compose.yml`. Use any Postgres client if you don't have `psql` installed — pgAdmin, DBeaver, or Visual Studio's own SQL Server Object Explorer-equivalent for Postgres all work.)

## Step 6 — run the isolation proof test

```bash
cd ../DevLog.Tests
dotnet test
```

Both tests need Steps 1, 4, and 5 done first. `TenantA_Cannot_See_TenantB_RawEntries` is the actual Sprint 0 exit criterion from the checklist — don't move on to building features until this passes.

## Step 7 — Clerk configuration

Before the API can authenticate anything: in `DevLog.Api/appsettings.Development.json`, replace `Clerk:Authority` with your real Clerk instance's issuer URL, and `Clerk:AuthorizedParty` with your frontend's origin (`http://localhost:3000` for local dev). This is also the spike moment from the Sprint 0 checklist — get a real Clerk-issued test token authenticating against `GET /api/portfolio/me` before building anything else on top of auth. Clerk validates the `azp` claim rather than a conventional `aud` claim, which is the one piece of this setup most worth double-checking early.

## Step 8 — run the frontend

```bash
cd Devlog.os/web
npm install
npm run dev
```

Nothing meaningful renders yet — this just confirms the scaffold runs. The first real page (Home) is the next build milestone after Step 6 passes, per the roadmap's build order.

---

## Milestone 2 — Sprint Journal entries + anonymization pipeline

New since Step 8: `RawEntry.SanitizedPreview`/`SanitizationModelVersion` fields, an `ILlmProvider`/`AnthropicLlmProvider` LLM Gateway, a `SanitizationPipelineService`, and `EntriesController` (`POST /api/entries`, `GET /api/entries/{id}`, `POST /api/entries/{id}/sanitize`, `POST /api/entries/{id}/approve`).

### Step 9 — new migration for the entity change

```bash
cd Devlog.os/api/DevLog.Api
dotnet ef migrations add AddSanitizedPreviewToRawEntry --project ../DevLog.Infrastructure --startup-project .
dotnet ef database update --project ../DevLog.Infrastructure --startup-project .
```

### Step 10 — Anthropic API key

Get a key from the Anthropic Console (console.anthropic.com → API Keys), then in `DevLog.Api/appsettings.Development.json` replace the `Anthropic:ApiKey` placeholder with it. Nothing else in that section needs changing — `claude-haiku-4-5` is the model used for sanitization (a structured find-and-replace task, not open-ended generation, so the cheaper/faster tier is the right fit per the architecture doc's cost strategy).

### Step 11 — smoke-test the pipeline end to end

With the API running and a valid Clerk bearer token (same as Step 7):

```bash
# 1. Create a draft entry
curl.exe -X POST https://localhost:<port>/api/entries `
  -H "Authorization: Bearer <token>" -H "Content-Type: application/json" `
  -d '{"entryType":"SprintEntry","rawContent":"Fixed a checkout latency bug at Acme Corp by rewriting the PaymentSync service..."}'

# 2. Sanitize it (use the id returned above)
curl.exe -X POST https://localhost:<port>/api/entries/<id>/sanitize -H "Authorization: Bearer <token>"

# 3. Review the sanitizedPreview field in the response, then approve
curl.exe -X POST https://localhost:<port>/api/entries/<id>/approve -H "Authorization: Bearer <token>"
```

Confirm the entry Acme Corp/PaymentSync references got replaced with generic descriptors in `sanitizedPreview`, and that a row landed in `published_entries` and `sanitization_audit`.

### Handoff — commit and push

```bash
git add .
git commit -m "Add Sprint Journal entry creation + Anthropic-backed anonymization pipeline"
git push
```

---

## Milestone 3 — Home + Experience pages (first real UI)

New: `web/src/app/[slug]/` (dynamic route per creator), a shared layout with persistent nav + Ask AI widget + Request Interview button, `web/src/lib/mock-portfolio.ts` (stand-in for the real API until `GET /api/portfolio/:slug` exists), and Home/Experience page components under `web/src/components/portfolio/`.

### Step 12 — run it

```bash
cd Devlog.os/web
npm run dev
```

Then visit:
- `http://localhost:3000/chika` — Home, fully-staffed demo
- `http://localhost:3000/chika/experience` — Experience timeline
- `http://localhost:3000/new-grad-demo` — Home with the "no job yet" state (no title, no years-of-experience stat)

Verified in the sandbox: `tsc --noEmit` and `eslint` both clean. `next build` itself wasn't verified there (sandbox has no network access to fetch the Linux SWC binary) — your local `npm run build` is the real first check, same caveat pattern as the C# backend.

### Handoff — commit and push

```bash
git add .
git commit -m "Add Home and Experience pages (mock-data backed, first real UI)"
git push
```

---

## Milestone 4 — embedding pipeline + real Ask AI

New: `Embedding`/`AiQueryLog` entities (pgvector-backed), `IEmbeddingProvider`/`OpenAiEmbeddingProvider` (OpenAI `text-embedding-3-small`), `ILlmProvider.AnswerQuestionAsync` with both an `AnthropicLlmProvider` and an `OpenAiLlmProvider` implementation (switchable via `Ai:GenerationProvider`, see Step 15), `EmbeddingPipelineService` (wired into `SanitizationPipelineService.ApproveAsync`), `AskAiService`/`AskAiController` (`POST /api/portfolio/{slug}/ask`, anonymous), CORS, and `sql/002_public_read_policies.sql`. The frontend's Ask AI (Home hero + floating widget) now calls this endpoint for real instead of showing a placeholder.

### Note on a sandbox quirk (already fixed, no action needed)

Files edited more than once in the same build session were briefly landing corrupted on disk — either truncated mid-line, or padded with leftover null bytes past the real end of the file — even though the edit tool reported them as complete each time. Root cause: rewriting a file to shorter content didn't always truncate the old, longer file underneath it. Confirmed and fixed for every affected file (`[slug]/layout.tsx`, `AskAiForm.tsx`, `AskAiWidget.tsx`, `Hero.tsx`, `AnthropicLlmProvider.cs`) by writing them through a different path and verifying byte-for-byte. `tsc --noEmit` and `eslint` both come back clean now. Mentioned here in case you'd already opened one of these files and saw something odd — it's resolved.

### Step 13 — new migration (pgvector + embeddings + ai_query_logs)

New NuGet packages referenced: `Pgvector` (Domain), `Pgvector.EntityFrameworkCore` + `Microsoft.Extensions.Http` (Infrastructure, if not already restored from Milestone 2) — `dotnet restore` picks these up automatically.

```bash
cd Devlog.os/api/DevLog.Api
dotnet ef migrations add AddEmbeddingsAndAiQueryLogs --project ../DevLog.Infrastructure --startup-project .
dotnet ef database update --project ../DevLog.Infrastructure --startup-project .
```

This migration includes `CREATE EXTENSION IF NOT EXISTS vector` (via `HasPostgresExtension`) — the docker-compose Postgres image already has pgvector built in, this just enables it for the `devlog` database.

### Step 14 — apply the new RLS policies

```bash
psql -h localhost -U devlog -d devlog -f ../sql/002_public_read_policies.sql
```

### Step 15 — OpenAI API key + CORS check

Get a key from platform.openai.com → API keys, then in `DevLog.Api/appsettings.Development.json` replace `OpenAi:ApiKey`. This one key now covers both embeddings and generation — see the "generation provider is configurable" note below. Also confirm `Cors:FrontendOrigins` includes wherever your Next.js dev server actually runs (defaults to `http://localhost:3000`).

**Generation provider is configurable (added 2026-07-05).** `Ai:GenerationProvider` in `appsettings.Development.json` picks which vendor answers sanitization and Ask AI requests — `"OpenAI"` (default, uses `OpenAi:GenerationModel`, currently `gpt-4o-mini`) or `"Anthropic"` (uses the `Anthropic` section). Added because Anthropic console API key access was blocked at the time — if that gets sorted later, switching back is just flipping this one value back to `"Anthropic"`, no code changes. Only the configured provider's API key actually needs to be real; the other section's placeholder can stay as-is.

### Step 16 — frontend env var

```bash
cd Devlog.os/web
cp .env.local.example .env.local
```

Edit `.env.local` if your API doesn't run on `http://localhost:5000`.

### Step 17 — smoke-test end to end

1. Create, sanitize, and approve an entry (Milestone 2's Step 11) so at least one `published_entries` row exists and gets embedded.
2. Run both apps: `dotnet run` (API) and `npm run dev` (web).
3. Visit `http://localhost:3000/<your-real-slug>` and ask a question in the hero's Ask AI box about something the approved entry actually covers.
4. Confirm you get a real answer (not the old placeholder text), and that asking something unrelated gets an honest "I don't see specific evidence of that" instead of a hallucinated answer.

### Handoff — commit and push

```bash
git add .
git commit -m "Add embedding pipeline and real Ask AI (RAG over pgvector, OpenAI embeddings, configurable generation provider)"
git push
```

---

## Framework upgrade — .NET 8 → .NET 10 (2026-07-05)

Triggered by a NuGet downgrade error on `Pgvector.EntityFrameworkCore`. While fixing it, checked actual .NET support dates: .NET 8 and .NET 9 both go out of support November 10, 2026 (Microsoft extended .NET 9's STS window to 24 months, landing it on the same date as .NET 8's LTS end). .NET 10 is the current LTS, supported through November 2028 — so rather than patch around the version conflict, the whole solution moved to .NET 10.

**What changed:** `TargetFramework` is now `net10.0` in all four projects. Package versions bumped to their 10.x releases (verified against NuGet's actual published version lists, not guessed): `Microsoft.EntityFrameworkCore`/`.Design` → 10.0.8, `Npgsql.EntityFrameworkCore.PostgreSQL` → 10.0.1, `EFCore.NamingConventions` → 10.0.1, `Microsoft.Extensions.Http` → 10.0.8, `Microsoft.AspNetCore.Authentication.JwtBearer` → 10.0.8, `Microsoft.NET.Test.Sdk` → 17.14.1, `xunit` → 2.9.3. `Pgvector.EntityFrameworkCore` moved back to `0.3.0` (the latest release) since Npgsql 10.0.1 now satisfies its `>= 9.0.1` requirement — the 0.2.2 pin from earlier is gone.

### Step 18 — install the .NET 10 SDK if you don't have it, then rebuild

Visual Studio 2026 should already support .NET 10 (check Tools → Get Tools and Features → verify the .NET 10 SDK component is installed, or run `dotnet --list-sdks` in a terminal). Then:

```bash
cd Devlog.os/api
dotnet restore
dotnet build
```

If restore/build come back clean, everything from Steps 1–17 still applies unchanged — this was a framework/package version change only, no application logic changed.

### Handoff — commit and push

```bash
git add .
git commit -m "Upgrade solution to .NET 10 / EF Core 10"
git push
```

---

## Milestone 5 — Request Interview (last Phase 1 core feature)

New: `InterviewRequest` entity (`interview_requests` table), `InterviewRequestService` (create/accept/decline), `InterviewRequestsController` (`POST /api/portfolio/{slug}/interview-requests`, anonymous — recruiter submission) and `CreatorInterviewRequestsController` (`GET /api/interview-requests`, `POST /api/interview-requests/{id}/accept`, `POST /api/interview-requests/{id}/decline`, all `[Authorize]`), `sql/003_interview_requests_policies.sql`. The frontend's `RequestInterviewButton` (in the persistent nav) is now a real form — recruiter email, optional name, a note — instead of the "not wired up yet" placeholder.

No creator inbox UI ships with this milestone, deliberately — the roadmap's Phase 1 scope is "instrument everything, ship no dashboard... query the tables directly for now." The creator analytics dashboard (which would surface this inbox visually) is a Phase 2 item. For now, act on requests via `GET /api/interview-requests` and the accept/decline endpoints directly (curl/Postman), same as Milestone 2's entry pipeline before any UI existed for it.

Accepting a request is the only thing that reveals contact info to the recruiter — the creator supplies `contactInstructions` (a scheduling link, email, whatever they choose) in the accept call itself, matching the architecture doc's "DevLog itself doesn't broker contact info without that explicit accept step."

### Step 19 — new migration (interview_requests)

```bash
cd Devlog.os/api/DevLog.Api
dotnet ef migrations add AddInterviewRequests --project ../DevLog.Infrastructure --startup-project .
dotnet ef database update --project ../DevLog.Infrastructure --startup-project .
```

### Step 20 — apply the new RLS policies

```bash
psql -h localhost -U devlog -d devlog -f ../sql/003_interview_requests_policies.sql
```

This one's different in shape from 001/002: it grants both a creator-scoped `tenant_isolation` policy (read/update your own tenant's requests) *and* a `public_insert` policy (anonymous recruiter submission, gated on the target portfolio not being Paused) — the first RLS policy in this schema that grants anonymous write access, not just anonymous read.

### Step 21 — smoke-test end to end

```bash
# 1. Anonymous recruiter submits a request (no auth header)
curl.exe -X POST https://localhost:<port>/api/portfolio/<your-slug>/interview-requests `
  -H "Content-Type: application/json" `
  -d '{"recruiterEmail":"recruiter@example.com","recruiterName":"Jordan","note":"Loved the Ask AI answer on your Postgres RLS work — got 20 min this week?"}'

# 2. Creator checks the inbox (real Clerk bearer token, same as Step 7)
curl.exe https://localhost:<port>/api/interview-requests -H "Authorization: Bearer <token>"

# 3. Accept it (use the id from step 2) — this is what reveals contact info
curl.exe -X POST https://localhost:<port>/api/interview-requests/<id>/accept `
  -H "Authorization: Bearer <token>" -H "Content-Type: application/json" `
  -d '{"contactInstructions":"Grab time here: https://cal.com/your-handle"}'
```

Confirm step 1 succeeds with no auth header at all (proves the `public_insert` RLS policy is working, not just app-layer logic), and that a second creator's token can't see or accept the first creator's request (proves `tenant_isolation` still holds for a brand-new table, same as the very first isolation test).

### Handoff — commit and push

```bash
git add .
git commit -m "Add Request Interview (last Phase 1 core feature)"
git push
```

---

## Milestone 6 — email integration (creator notification on new requests)

New: `Portfolio.NotificationEmail` (creator-supplied, optional), `IEmailProvider`/`PostmarkEmailProvider` (Infrastructure/Email/), and `InterviewRequestService.CreateAsync` now sends a best-effort notification email to the creator on every new interview request instead of leaving Milestone 5's "no Hangfire yet, creator polls the inbox" gap unfilled. Scoped deliberately narrow: this covers creator notification only, not recruiter email verification (the other Phase-1 security item that touches email) — that's still open.

The send is synchronous, inline on the request path (same "no Hangfire queue yet" simplification as sanitization and everything else in this codebase), and deliberately best-effort: if `NotificationEmail` isn't set, or the Postmark call fails for any reason, the recruiter's submission still succeeds and the request is still saved — the creator can always fall back to `GET /api/interview-requests`. Failures are logged, not surfaced to the recruiter.

### Step 22 — new migration (Portfolio.NotificationEmail)

```bash
cd Devlog.os/api/DevLog.Api
dotnet ef migrations add AddNotificationEmailToPortfolio --project ../DevLog.Infrastructure --startup-project .
dotnet ef database update --project ../DevLog.Infrastructure --startup-project .
```

### Step 23 — Postmark setup

Get a Server Token from your Postmark account (Servers → your server → API Tokens), then in `DevLog.Api/appsettings.Development.json` replace `Postmark:ApiKey`. Also replace `Postmark:FromAddress` with a Sender Signature (or domain) you've verified in the Postmark dashboard — Postmark rejects sends from an unverified From address outright, it won't silently accept it.

Since there's no portfolio-update endpoint yet (`PortfolioController` only has `GET /me` — a pre-existing gap, not new to this milestone), set your own `NotificationEmail` directly for now:

```sql
UPDATE portfolios SET notification_email = 'you@example.com' WHERE tenant_id = (SELECT id FROM tenants WHERE slug = '<your-slug>');
```

### Step 24 — smoke-test

Repeat Milestone 5's Step 21, part 1 (submit an interview request anonymously) against a tenant whose `NotificationEmail` is now set, and confirm the email arrives. If it doesn't, check the API logs for the `LogWarning` `InterviewRequestService` emits on send failure before assuming the request itself failed — it won't have.

### Handoff — commit and push

```bash
git add .
git commit -m "Add email integration: creator notification on new interview requests (Postmark)"
git push
```

---

## Milestone 7 — real onboarding (no more hand-inserted rows)

This turned out to be a bigger fix than "add a signup form." Tracing through the existing code: `TenantResolutionMiddleware` only ever read a custom `tenant_id` JWT claim, but nothing anywhere ever configured Clerk to issue that claim — so it was always empty for a real Clerk-issued token. That meant every authenticated write endpoint built so far (`EntriesController`, `CreatorInterviewRequestsController`, `PortfolioController`) would have returned `Unauthorized`/empty for a real signed-in user, not just for lack of a signup form. This milestone fixes that at the root.

New: `Tenant.UserId` is now a `string` (Clerk's `sub` claim — `"user_2abc..."` — never fit in the old `Guid`), with a unique index. `TenantProvisioningService` (Infrastructure/MultiTenancy/) auto-creates a `Tenant` + placeholder `Portfolio` the first time a Clerk-authenticated request arrives from a user with no existing row, keyed on `sub` — no webhook, no Clerk dashboard changes, matching this project's running preference for the simpler synchronous option. `TenantResolutionMiddleware` now calls it instead of reading the dead custom claim. `sql/004_portfolios_policy.sql` adds RLS to `portfolios`, which never actually got a policy in 001-003 — a real gap, not a deferral: `GetMyPortfolio`'s "no WHERE clause, RLS enforces scope" comment was false with zero policies on that table, and it stayed invisible only because the dev database never had more than one portfolio row until now. `PortfolioController.GetMyPortfolio` now filters explicitly by tenant id (required once a `public_read` policy means more than one row is legitimately visible to any given caller), and there's a new `PUT /api/portfolio/me` so `NotificationEmail` and everything else on a portfolio can finally be set through the API instead of SQL.

### Step 25 — new migration (Tenant.UserId type change + unique index)

```bash
cd Devlog.os/api/DevLog.Api
dotnet ef migrations add ChangeTenantUserIdToString --project ../DevLog.Infrastructure --startup-project .
dotnet ef database update --project ../DevLog.Infrastructure --startup-project .
```

Before running this, clean up the synthetic `chika` tenant/portfolio rows from the Milestone 6 smoke test — they have a random GUID in `user_id`, which won't match any real Clerk `sub` and will just sit there as clutter (or collide oddly with the type change):

```sql
DELETE FROM portfolios WHERE tenant_id = (SELECT id FROM tenants WHERE slug = 'chika');
DELETE FROM tenants WHERE slug = 'chika';
```

### Step 26 — apply the new RLS policy

```bash
psql -h localhost -U devlog -d devlog -f ../sql/004_portfolios_policy.sql
```

### Step 27 — smoke-test end to end

1. Sign in via your real Clerk-hosted flow and get a real bearer token (same as Step 7).
2. Hit any authenticated endpoint for the first time — `GET /api/portfolio/me` is the simplest:
   ```bash
   curl.exe https://localhost:<port>/api/portfolio/me -H "Authorization: Bearer <token>"
   ```
   This should now return a freshly auto-provisioned portfolio (`"headline": "New DevLog member"`, a `dev-xxxxxxxx` placeholder slug) instead of 401/empty — confirm a `tenants` row and a `portfolios` row both exist for you in the database too.
3. Set your real profile data and `NotificationEmail` through the new update endpoint instead of SQL:
   ```bash
   curl.exe -X PUT https://localhost:<port>/api/portfolio/me `
     -H "Authorization: Bearer <token>" -H "Content-Type: application/json" `
     -d '{"headline":"Backend engineer building DevLog","notificationEmail":"you@example.com","visibility":"Unlisted"}'
   ```
4. Re-run Milestone 5's interview-request smoke test against your real slug (from step 2's response) instead of `chika`, and confirm the notification email still arrives — this proves the whole chain (auto-provisioned tenant → real portfolio → public anonymous lookup by slug → notification) works end to end without any manual SQL.

### Handoff — commit and push

```bash
git add .
git commit -m "Add real onboarding: auto-provision Tenant/Portfolio on first Clerk request, fix missing portfolios RLS, add portfolio update endpoint"
git push
```

---

## Milestone 8 — RLS force-on-owner fix (the app stops connecting as a superuser)

This is a correction, not just a fix. Every RLS script since `001` carried the same note: "needs a non-owner app role + `FORCE ROW LEVEL SECURITY`." Checking that against how the official Postgres Docker image actually behaves: `POSTGRES_USER` (this project's `devlog`, set in `docker-compose.yml`) is created as a genuine Postgres **superuser**, not just a table owner. Postgres unconditionally disables row security for superusers — `FORCE ROW LEVEL SECURITY` doesn't touch that at all, it only affects a non-superuser owner. So every prior note was aiming at the wrong lever. The real fix: stop the app from connecting as a superuser, full stop.

New: `sql/005_create_app_role.sql` creates `devlog_app`, an ordinary non-superuser, non-owner role, with `SELECT`/`INSERT`/`UPDATE`/`DELETE` grants (plus `ALTER DEFAULT PRIVILEGES` so future migrations' tables are covered automatically, not just what exists today). `DevLogDbContextFactory` (new — an `IDesignTimeDbContextFactory<DevLogDbContext>`) keeps `dotnet ef` commands running as the owner (`devlog`, via `ConnectionStrings:DevLogDb`) since DDL needs those rights; `ServiceCollectionExtensions.AddDevLogInfrastructure` now points real app traffic at a new `ConnectionStrings:DevLogDbRuntime` connection instead, using `devlog_app`. `TenantIsolationTests` now connects as `devlog_app` too — worth being direct about what that means: every previous run of that "Sprint 0 exit criterion" test, from Sprint 0 through Milestone 7, connected as the superuser and could only ever have proven the policy SQL was internally consistent, not that RLS was blocking anything for the role real traffic actually uses. This is the first version of the test that can actually fail the way it's supposed to. Fixing it also surfaced a real bug in the test's own seed step (a mismatched tenant context that RLS's `WITH CHECK` would now correctly reject) — fixed alongside.

### Step 28 — create the restricted role

```bash
psql -h localhost -U devlog -d devlog -f ../sql/005_create_app_role.sql
```

Run this after all migrations and `sql/001` through `sql/004` are applied.

### Step 29 — re-run the isolation test for real

```bash
cd Devlog.os/api/DevLog.Tests
dotnet test
```

Both tests must still pass — but this time they're actually proving something. If either fails now and didn't before, that's the fix working as intended: a real policy gap that superuser access was silently hiding.

### Step 30 — confirm the app still runs against the restricted role

```bash
cd Devlog.os/api/DevLog.Api
dotnet run
```

Exercise a few endpoints (Ask AI, `GET /api/portfolio/me`, submitting an interview request) to confirm nothing broke — the app should behave identically from the outside, since `devlog_app` has exactly the privileges the app ever legitimately needed.

### Handoff — commit and push

```bash
git add .
git commit -m "Fix RLS force-on-owner gap: app connects as a restricted non-superuser role, not the migration owner"
git push
```

---

## Milestone 9 — Sprint Journal page + output sanitization

Asked to close the "output sanitization on rendered entries" security item, but tracing it found there was nothing to sanitize yet — no Sprint Journal page existed at all (`/journal` was still Milestone 3's disabled "Coming soon" nav item), no entry-rendering component, no markdown library installed. Scoped up to building the real page, sanitized from day one, rather than shipping inert safety infrastructure with nothing using it yet.

New backend: `RawEntry.Title` / `PublishedEntry.Title` (no title field existed anywhere in this schema before now — entries were designed as a single free-text blob from the start) and `PublishedEntry.SourceEntryType` (a denormalized copy of `RawEntry.EntryType`, not a join — `raw_entries` has zero RLS grant for the anon/public role, so the new public endpoint below can never join through to it). `EntriesController`'s `CreateEntryRequest` now requires `title`. New `SprintJournalController` — `GET /api/portfolio/{slug}/entries` (listing, `SourceEntryType == SprintEntry` only) and `GET /api/portfolio/{slug}/entries/{id}` (detail, includes the full `sanitizedContent`), both anonymous, same portfolio-by-slug + visibility-check pattern as `AskAiService`/`InterviewRequestService`.

New frontend: `react-markdown` + `rehype-sanitize` + `remark-gfm` (pinned to 10.1.0/6.0.0/4.0.1, checked against the npm registry). `SanitizedMarkdown.tsx` is the actual fix — `react-markdown` parses markdown into real React elements and never calls `dangerouslySetInnerHTML` unless the `rehype-raw` plugin is added, which is deliberately never imported anywhere in this codebase; `rehype-sanitize`'s default allowlist schema adds a second, independent layer on top (blocks `javascript:` URIs in markdown links/images, event-handler attributes, and any `<style>`/`<script>` a plugin might otherwise introduce). Styled with explicit Tailwind utility classes via `react-markdown`'s `components` map, not the `@tailwindcss/typography` plugin's `prose` classes — that plugin isn't installed anywhere else in this project, and this was the only place that would have needed it. New pages: `[slug]/journal/page.tsx` (listing) and `[slug]/journal/[entryId]/page.tsx` (detail). `PortfolioNav`'s Journal link is enabled now; `ExperienceTimeline`'s "coming soon" placeholder link now goes to the journal for real (entries and experiences aren't associated with each other in the schema, so it links to the index, not a filtered view).

Also new: `next.config.ts` now sets a `Content-Security-Policy` header on every response (architecture doc Section 9's second half of this fix, defense in depth alongside the renderer). `script-src`/`style-src` allow `'unsafe-inline'` deliberately — Next.js App Router hydration and Tailwind's critical CSS both rely on it, and a strict nonce-based policy is bigger scope than this milestone. Verify in an actual browser once running (`npm run dev`, check the console for CSP violations) — this is config the sandbox can't render and check itself, same caveat pattern as `next build`.

### Step 31 — new migration (Title + SourceEntryType)

```bash
cd Devlog.os/api/DevLog.Api
dotnet ef migrations add AddTitleAndSourceEntryTypeToEntries --project ../DevLog.Infrastructure --startup-project .
dotnet ef database update --project ../DevLog.Infrastructure --startup-project .
```

### Step 32 — install the new frontend packages

```bash
cd Devlog.os/web
npm install
```

`package.json` already lists `react-markdown`/`rehype-sanitize`/`remark-gfm` — this just needs to actually pull them into your local `node_modules`.

### Step 33 — smoke-test end to end

1. Create, sanitize, and approve a `SprintEntry` (Milestone 2's Step 11, now with a `title` in the create request) so at least one row exists with `SourceEntryType = SprintEntry`.
2. `npm run dev` and visit `http://localhost:3000/<your-slug>/journal` — confirm your entry appears, then click into it and confirm the markdown (headings, lists, a code block if your entry has one) renders correctly styled, not as raw text.
3. Open browser dev tools → Network → pick the page request → confirm a `Content-Security-Policy` response header is present.
4. As a deliberate check, try approving an entry whose raw content includes something like `` `<script>alert(1)</script>` `` or a markdown link with a `javascript:` URI, and confirm it renders as inert text/is stripped, not executed.

### Handoff — commit and push

```bash
git add .
git commit -m "Add Sprint Journal page with sanitized markdown rendering + CSP header"
git push
```

---

## Milestone 10 — recruiter email verification (last Phase 1 security item)

Closes the last item on the original Phase 1 security list: nothing previously confirmed a recruiter's email address was real before a creator got notified about it — Milestone 6 sent the creator notification straight from `CreateAsync`, so a fabricated or mistyped address looked identical to a real one. This adds a verify-before-notify step: submitting a request now emails the recruiter a one-time confirmation link, and the creator only finds out once that link is clicked.

New backend: `InterviewRequest` gains `EmailVerified` (bool), `VerificationToken` (a 64-char random hex string, `RandomNumberGenerator.GetHexString(64)`), and `VerificationTokenExpiresAt` (24-hour lifetime) — deliberately orthogonal to `Status`: verification answers "is this request real," `Status` answers "what did the creator decide," and a request can be verified-but-pending or (in theory) verified-and-declined. `InterviewRequestService.CreateAsync` no longer calls `NotifyCreatorAsync` directly — it generates the token and calls new `SendVerificationEmailAsync`, which emails the recruiter a link to `{Frontend:BaseUrl}/verify-interview-request` carrying `slug`, `requestId`, and `token` as query params. New `VerifyEmailAsync(requestId, token)` — anonymous, no tenant context, the token itself is the credential — checks the token matches and hasn't expired, flips `EmailVerified`, clears the token fields, and only then calls `NotifyCreatorAsync` (moved here from `CreateAsync`). New `POST /api/portfolio/{slug}/interview-requests/{id}/verify` on `InterviewRequestsController` (anonymous, same trust boundary as `Create`) wraps it. `CreatorInterviewRequestsController.GetInbox` now filters `WHERE EmailVerified` — this is real anti-abuse filtering, not just defense in depth: an unconfirmed request never appears in the creator's inbox at all, not even temporarily. New `FrontendOptions` (`Frontend:BaseUrl` in config) gives the backend the frontend's own base URL to build the confirmation link with, since that link is emailed, not returned in an API response.

New frontend: `web/src/app/verify-interview-request/page.tsx` — a client component (query-param driven, wrapped in `Suspense` per Next.js's `useSearchParams` requirement) that reads `slug`/`requestId`/`token`, POSTs to the verify endpoint, and shows a confirmed / already-confirmed / expired / invalid-link / error state. `RequestInterviewButton`'s success copy now says to check email for a confirmation link instead of implying the creator was already notified.

### Step 34 — new migration (email verification fields)

```bash
cd Devlog.os/api/DevLog.Api
dotnet ef migrations add AddEmailVerificationToInterviewRequests --project ../DevLog.Infrastructure --startup-project .
dotnet ef database update --project ../DevLog.Infrastructure --startup-project .
```

### Step 35 — set the frontend base URL

`appsettings.Development.json` already has a `Frontend:BaseUrl` section defaulted to `http://localhost:3000` — only change it if your Next.js dev server runs somewhere else.

### Step 36 — smoke-test end to end

```bash
# 1. Anonymous recruiter submits a request (same as Milestone 5's Step 21)
curl.exe -X POST https://localhost:<port>/api/portfolio/<your-slug>/interview-requests `
  -H "Content-Type: application/json" `
  -d '{"recruiterEmail":"recruiter@example.com","recruiterName":"Jordan","note":"Got 20 min this week?"}'

# 2. Check the recruiter's inbox for "Confirm your DevLog interview request" — confirm no
#    creator-notification email arrives yet, and the request does NOT show up in:
curl.exe https://localhost:<port>/api/interview-requests -H "Authorization: Bearer <token>"

# 3. Click the link in the email (lands on /verify-interview-request), or call the endpoint
#    directly with the id from step 1's response and the token from the email URL:
curl.exe -X POST https://localhost:<port>/api/portfolio/<your-slug>/interview-requests/<id>/verify `
  -H "Content-Type: application/json" -d '{"token":"<token-from-email-link>"}'

# 4. Now check both: the creator-notification email should have just arrived, and the
#    request should now appear in GET /api/interview-requests.
```

Also confirm clicking the same link twice returns `alreadyVerified: true` rather than erroring, and that a token past 24 hours (or a tampered one) returns the expired/invalid state on the frontend page.

### Handoff — commit and push

```bash
git add .
git commit -m "Add recruiter email verification: verify-before-notify on interview requests"
git push
```

---

## Milestone 11 — creator dashboard (the app becomes actually usable)

Triggered by "I want to start using this for myself and partner" — investigating what that actually required surfaced that Phase 1, despite being "feature-complete," had no way for a creator to do anything except curl. Three real gaps, not one:

1. **No creator UI at all.** Writing entries, editing your profile/experience, and managing interview requests were curl-only — every frontend page built so far was the recruiter/viewer side.
2. **The public Home/Experience pages were still 100% mock data.** `lib/mock-portfolio.ts` was never actually swapped for a real fetch, despite its own comment promising that since Milestone 3 — `GET /api/portfolio/:slug` didn't exist until now.
3. **`experiences` had zero RLS policy anywhere in `sql/001-005`** — not "missing public_read" like `portfolios` was in Milestone 7, genuinely no policy at all. Latent until now because nothing ever queried the table through the app.

Also found and fixed in the process: **enums were never serializing as strings.** Nothing in `Program.cs` ever configured `JsonStringEnumConverter`, so the API's actual default behavior all along was serializing `PortfolioVisibility.Unlisted` as `1`, not `"Unlisted"` — meaning every prior curl example in this file that sent an enum as a JSON string (Milestone 2's `"entryType":"SprintEntry"`, Milestone 7's `"visibility":"Unlisted"`) would have failed to deserialize. Fixed globally in `Program.cs` rather than per-DTO.

New backend: `Portfolio.DisplayName`/`PositioningLine`/`PhotoUrl` and `Experience.Title` (fields the frontend has needed since Milestone 3 but the entities never carried). `sql/006_experiences_policy.sql` (tenant_isolation + public_read). New `ExperienceController` (`GET/POST/PUT/DELETE /api/experiences`, creator-authenticated CRUD). New `GET /api/entries` (list mine) on `EntriesController`. New public `GET /api/portfolio/{slug}` on `PortfolioController` (`[AllowAnonymous]`, returns portfolio + experiences in one response). `GetMyPortfolio` now returns a shaped response including `slug` (previously the raw entity, which had no way to expose `Tenant.Slug`).

New frontend: `@clerk/nextjs` (finally — no frontend auth existed before this milestone, only backend JWT *validation*). `src/middleware.ts` (`clerkMiddleware`, protects `/dashboard(.*)`). `ClerkProvider` wraps the root layout. `lib/use-api.ts` — a `useApi()` hook that attaches the live Clerk session token as a Bearer header on every dashboard API call. New `/dashboard` area: an overview page, `/dashboard/profile` (edit everything on Portfolio), `/dashboard/experience` (list/add/edit/delete), `/dashboard/journal` (list entries + a composer: write → sanitize → review → approve), `/dashboard/interview-requests` (inbox + accept/decline). `lib/mock-portfolio.ts` now does a real `fetch` to `GET /api/portfolio/:slug` — kept the filename and function signature unchanged since every caller (`[slug]/layout.tsx`, `[slug]/page.tsx`, `[slug]/experience/page.tsx`) needed zero changes as a result. Also fixed a port mismatch in `web/.env.local` (`NEXT_PUBLIC_API_BASE_URL` pointed at the HTTPS port from `launchSettings.json` with an `http://` scheme — every frontend→API call would have failed to connect).

**Sandbox limitation, not a code defect:** `@clerk/nextjs` could not actually be installed in the build sandbox — every `npm install` attempt hit `ENOTEMPTY` on an unrelated stale `@img/sharp-libvips-linux-x64` temp-rename directory from an earlier session, and it couldn't be cleared (permission-denied on a `.so` file inside it, consistent with something on your actual machine still holding it open). `package.json` was hand-edited to add `"@clerk/nextjs": "7.6.1"` the same way `react-markdown` was added in Milestone 9 when `npm install` hit a similar wall. `tsc --noEmit` confirms every new/changed file is otherwise clean — the only errors are "cannot find module '@clerk/nextjs'", which resolves the moment `npm install` actually runs somewhere that isn't blocked.

### Step 37 — new migration (dashboard fields)

```bash
cd Devlog.os/api/DevLog.Api
dotnet ef migrations add AddDashboardFieldsToPortfolioAndExperience --project ../DevLog.Infrastructure --startup-project .
dotnet ef database update --project ../DevLog.Infrastructure --startup-project .
```

### Step 38 — apply the new RLS policy

```bash
psql -h localhost -U devlog -d devlog -f ../sql/006_experiences_policy.sql
```

### Step 39 — Clerk frontend keys

Clerk dashboard → API Keys. Paste the real `Publishable key` and `Secret key` into `web/.env.local`'s `NEXT_PUBLIC_CLERK_PUBLISHABLE_KEY`/`CLERK_SECRET_KEY` (placeholders are already there). This is a different pair from the backend's `Clerk:Authority`/`Clerk:AuthorizedParty` — same Clerk instance, but the frontend half (signs the user in, mints the token) vs. the backend half (validates a token that already exists).

### Step 40 — install the new frontend package

```bash
cd Devlog.os/web
npm install
```

`package.json` already lists `@clerk/nextjs` — this actually pulls it into `node_modules` (never completed in the sandbox, see the note above).

### Step 41 — smoke-test the whole creator loop, for real

```bash
cd Devlog.os/api/DevLog.Api
dotnet run
```
```bash
cd Devlog.os/web
npm run dev
```

1. Visit `http://localhost:3000`, click **Sign in**, and sign in via Clerk. You should land back on the root page with a **Go to dashboard** button.
2. `/dashboard` → confirm it loads your (auto-provisioned) portfolio and shows your `devlog.app/<slug>` link.
3. `/dashboard/profile` → fill in Display name, Positioning line, Current role, Core technologies. Save, then reload the page and confirm the values persisted.
4. `/dashboard/experience` → add one or two entries (an Employer role and a PersonalProject work well). Edit one, confirm the change sticks.
5. `/dashboard/journal` → write a new Sprint Journal entry with something identifying in it (a fake employer name), Sanitize it, confirm the sanitized preview actually stripped it, then Approve & publish.
6. Visit your real public portfolio at `http://localhost:3000/<your-slug>` — confirm Home and Experience now show what you just entered (not the old `chika`/`new-grad-demo` mock fixtures), and `/journal` shows the entry you just published.
7. From a different browser (or an incognito window, so you're not signed in as the creator), submit an interview request against your slug, verify it via the emailed link, then back in `/dashboard/interview-requests`, confirm it appears and Accept it with some contact instructions.

If any step returns a `401`, double check `web/.env.local`'s `NEXT_PUBLIC_API_BASE_URL` matches whatever port `dotnet run` actually printed — `launchSettings.json`'s ports can change if Visual Studio ever regenerates them.

### Handoff — commit and push

```bash
git add .
git commit -m "Add creator dashboard: Clerk frontend auth, Experience CRUD, real public portfolio endpoint, enum JSON fix"
git push
```

---

---

## Milestone 12 — closing the two deferred Phase 1 gaps (before onboarding other developers)

Triggered by "let's implement everything so we can onboard other developers soon." The roadmap review a few sessions back had deliberately deferred two Phase 1 security/instrumentation items — `portfolio_view_events` logging and enforcing `Subscription.SanitizationQuota` — on the reasoning that they're cost/abuse controls that only matter once strangers are using the product. That premise just changed, so both got closed now, before Milestone 13's onboarding automation adds real new-developer traffic.

**`portfolio_view_events`.** New entity + table, same internal-only shape as `ai_query_logs` (tenant-isolated, no public_read policy — nothing public ever reads it). Logged in `PortfolioController.GetPublicPortfolio` on every successful public page load: `TenantId`, a hashed viewer IP (`ViewerHash`, same SHA-256 construction as `AskAiController.HashRemoteIp` — no raw IP ever stored), and a timestamp. Best-effort: wrapped in a try/catch so a logging failure can never break an actual portfolio page load, same posture as `InterviewRequestService`'s notification email. No UI reads this yet — that's Milestone 16's creator analytics dashboard; this milestone just starts the data collection, per the roadmap's own "cheap to add alongside each feature as it's built" framing.

**Sanitization rate cap + quota enforcement.** New `sanitization_run_logs` table, logged once per real `SanitizeAsync` call (not just approved entries — a creator re-running sanitize on a draft is a real LLM cost each time). `SanitizationPipelineService.SanitizeAsync` now checks two things before calling the LLM: an hourly rate cap (`MaxRunsPerTenantPerHour = 10`, mirrors `AskAiService`'s existing hourly cap shape) and the existing `Subscription.SanitizationQuota` field (default 20/month) as an actual calendar-month enforcement — that field has existed since early in the build with a comment claiming it existed "specifically so a per-tenant rate cap can be enforced," but nothing ever counted against it until now. Both refusals throw a new `SanitizationLimitExceededException` (with a `Reason` distinguishing which limit was hit); `EntriesController.SanitizeEntry` catches it ahead of the existing generic `InvalidOperationException` handler and returns `429`, matching how `AskAiController` already handles its own rate limit.

**Scope note:** the `discipline`/multi-role expansion from Phase 2 was deliberately left out of this sequence — it's for onboarding non-developer roles (PM, BA, DevOps), and the stated goal here is onboarding other *developers*, so it doesn't serve that goal yet.

### Step 42 — new migration (view events + sanitization run logs)

```bash
cd Devlog.os/api/DevLog.Api
dotnet ef migrations add AddPortfolioViewEventsAndSanitizationRunLogs --project ../DevLog.Infrastructure --startup-project .
dotnet ef database update --project ../DevLog.Infrastructure --startup-project .
```

### Step 43 — apply the new RLS policy

```bash
psql -h localhost -U devlog -d devlog -f ../sql/007_view_events_and_sanitization_logs_policy.sql
```

(password: `devlog_local`, from `docker-compose.yml`)

### Step 43b — fix a real bug found in testing: anonymous inserts were silently rejected

Chika tested Step 44 below and found `portfolio_view_events` stayed empty despite loading the public page repeatedly. Root cause: `sql/007`'s `tenant_isolation` policy has no explicit `WITH CHECK`, so it falls back to using its `USING` expression for INSERT too — and an anonymous connection never has a real `app.current_tenant`, so every insert attempt failed RLS silently (the `catch { }` around it logged nothing). The same gap turned out to already exist in `ai_query_logs` since Milestone 4, undiscovered until now — `AskAiService`'s insert has no try/catch at all, so it could have been turning every Ask AI answer into a 500. Apply the fix, then rebuild and restart the API before re-testing:

```bash
psql -h localhost -U devlog -d devlog -f ../sql/008_fix_anonymous_insert_policies.sql
```

### Step 44 — smoke test

1. Load any public portfolio page (`http://localhost:3000/<slug>`) a couple of times, then `SELECT * FROM portfolio_view_events ORDER BY viewed_at DESC;` in psql — confirm a row per load.
2. Ask that portfolio's Ask AI widget a question and confirm you get a real answer back (not a 500) — this is what Step 43b's fix actually protects. Then `SELECT * FROM ai_query_logs ORDER BY created_at DESC;` and confirm a row landed.
3. In `/dashboard/journal`, sanitize the same draft entry 11 times in under an hour (re-sanitizing a Draft/PendingApproval entry is allowed). The 11th call should return `429` with `"Hourly sanitization limit reached. Try again later."` Confirm `SELECT * FROM sanitization_run_logs;` shows 10 rows for your tenant, not 11.
4. (Optional, to test the monthly quota path without waiting a month) temporarily lower a tenant's quota directly: `UPDATE subscriptions SET sanitization_quota = 2 WHERE tenant_id = '<your-tenant-id>';`, then confirm the 3rd sanitize call that hour returns `429` with `"Monthly sanitization quota reached."` — set it back to `20` afterward.

### Step 45 — bugfixes found while testing Milestone 12

Three real bugs surfaced during your own testing, all fixed now:

1. `portfolio_view_events`/`ai_query_logs` anonymous inserts were silently rejected by RLS (`sql/008_fix_anonymous_insert_policies.sql` — apply it the same way as `007`, `psql -h localhost -U devlog -d devlog -f ../sql/008_fix_anonymous_insert_policies.sql`).
2. Two compile errors (`ILogger<>` missing a `using` in `AskAiService.cs`; `TenantIsolationTests.cs` still passing a `Guid` to `Tenant.UserId`, which has been `string` since Milestone 7) — both fixed, no action needed beyond rebuilding.
3. **Ask AI answering "no evidence of C# experience" when your Experience timeline clearly lists it.** `EmbeddingPipelineService` only ever embedded Sprint Journal entries — Experience rows (where your actual tech stack lives) had no path into the `embeddings` table at all. Fixed by adding a second embedding source (`EmbeddingSourceType.Experience`), wired into `ExperienceController`'s create/update/delete. **No migration needed** — it's a string column, the new value just works.

**One-time backfill needed:** any Experience rows you created *before* this fix won't be embedded automatically. Open `/dashboard/experience`, edit each existing entry (even just re-saving with no changes works — Update always re-embeds), and save. New entries going forward embed automatically on create.

```bash
psql -h localhost -U devlog -d devlog -f ../sql/008_fix_anonymous_insert_policies.sql
```

Then rebuild, restart the API, backfill your Experience rows as above, and re-ask Ask AI the same question — should now correctly cite your tech stack.

### Handoff — commit and push

```bash
git add .
git commit -m "Close Phase 1 gaps: portfolio_view_events logging, sanitization rate cap + quota enforcement, Experience embeddings for Ask AI"
git push
```

---

## Milestone 13 — guided onboarding: GitHub import (first Phase 2 feature)

Triggered by "let's implement everything so we can onboard other developers soon." Two genuine forks decided via AskUserQuestion before building: GitHub import as a public-username lookup (no OAuth, no token storage — simpler and works for anyone, at the cost of a 60-req/hour unauthenticated GitHub rate limit shared across all imports) rather than a full OAuth connection; and resume upload deliberately deferred to its own follow-up milestone (13b) rather than bundled in here, since it needs file-upload handling and its own security pass.

A creator types their GitHub username on the new `/dashboard/import` page. The backend (`GitHubClient`, unauthenticated calls to GitHub's public REST API — requires a `User-Agent` header or GitHub rejects the request outright) fetches their top 5 non-fork public repos by stars/recency, plus README excerpts (capped at 4000 chars) for the top 3. `OnboardingImportService` flattens that into a text summary and asks the LLM (`ILlmProvider.SuggestOnboardingContentAsync`, new method, implemented on both Anthropic and OpenAI providers off one shared prompt in `LlmSystemPrompts`) to draft structured JSON: one suggested Experience entry per repo, plus a single AI-drafted Sprint Journal entry based on whichever repo has the most substantial README.

**Nothing is saved automatically.** `POST /api/onboarding/github-import` (`OnboardingController`, new) is read-only — it returns suggestions, full stop. The frontend shows each suggestion as a card with an explicit "Add"/"Use as draft" button that calls the *same existing* `POST /api/experiences` and `POST /api/entries` endpoints a creator would use typing by hand. A drafted Sprint Journal entry lands as an ordinary `Draft` — it still goes through the real sanitize → review → approve pipeline before anything publishes, exactly like the roadmap's "nothing bypasses the sanitize-and-approve pipeline" requirement. Experience suggestions skip that pipeline for the same reason hand-typed Experience entries always have: `CompanyLabel` is free text the creator reviews and can edit before saving, never auto-published from raw AI output.

Dates on suggested Experience entries come from GitHub's own repo metadata (`created_at`/`pushed_at`), not the LLM — more accurate than asking a model to guess, and one less thing that can hallucinate.

**No migration needed.** No schema changes at all this milestone — purely new services/controllers/a new frontend page.

### Step 46 — nothing to apply, just rebuild and restart

```bash
cd Devlog.os/api/DevLog.Api
dotnet build
dotnet run
```
```bash
cd Devlog.os/web
npm run dev
```

### Step 47 — smoke test

1. `/dashboard/import` → enter a real GitHub username with a few public repos (your own works well) → Import.
2. Confirm suggestions render: one card per repo (type, title, company/project label, dates, technologies, key achievement, a link back to the repo) plus one Sprint Journal draft card.
3. Click "Add to Experience" on one suggestion → confirm it appears on `/dashboard/experience` afterward, and that it got embedded (Ask AI should now be able to answer a question about it — same check as Milestone 12's Experience-embedding fix).
4. Click "Use as Sprint Journal draft" → confirm it appears as a `Draft` on `/dashboard/journal`, then run it through Sanitize → Approve like any hand-written entry.
5. Try a nonexistent GitHub username → confirm a clear 404 message, not a crash.

### Handoff — commit and push

```bash
git add .
git commit -m "Add Milestone 13: guided onboarding, GitHub import with AI-drafted suggestions"
git push
```

---

## Milestone 13b — guided onboarding: resume upload + AI extraction

Scheduled immediately after Milestone 13 at Chika's request ("we can defer for now, but I want it to be implemented immediately after this"). Two forks decided via AskUserQuestion: PDF only for now (not DOCX — one format, one extraction path, no format-detection branching); and the resume file itself gets stored and hosted (`Portfolio.CvFileUrl`), not just parsed and discarded, so uploading a resume also solves the pre-existing "no way to set a CV download link" gap.

A creator uploads a PDF on the same `/dashboard/import` page (new second section, below GitHub import). The file goes to `POST /api/onboarding/resume-import` (`OnboardingController`, new endpoint) as multipart form data, validated three ways before anything else happens: extension is `.pdf`, size is under 10MB, and the first bytes are the real `%PDF-` magic header (a renamed file doesn't get past this). The upload is buffered once into memory so it can be read twice: `IFileStorageProvider.SaveAsync` (new abstraction; `LocalDiskFileStorageProvider` is the only implementation for now — files land under `uploads/{tenantId}/resume.pdf`, served back out by a new `UseStaticFiles` mount in `Program.cs`) stores the exact bytes and updates `Portfolio.CvFileUrl` regardless of what happens next, then `PdfTextExtractor` (new, built on the `UglyToad.PdfPig` package — pure managed C#, MIT-licensed, chosen over sending the raw PDF straight to the LLM because neither `AnthropicLlmProvider` nor `OpenAiLlmProvider`'s request shape supports multi-part file content today) pulls the text layer out page by page.

**The critical difference from GitHub import: the resume-suggestion LLM prompt does its own anonymization.** GitHub READMEs are already public, so nothing needs scrubbing. A resume has real employer names in it, and unlike Sprint Journal entries, `Experience` rows never pass through `SanitizationPipelineService` — `CompanyLabel` is meant to be creator-typed free text already anonymized by the person typing it. Since there's no separate sanitize pass downstream to catch a real company name here, `LlmSystemPrompts.BuildResumeSuggestionSystemPrompt()` instructs the model to extract *and* generically-relabel companies (e.g. "a Series B fintech, payments team") in the same pass, with an explicit CRITICAL rule about never echoing a real employer name.

Same review-then-accept UX as GitHub import — `POST /api/onboarding/resume-import` never writes an Experience row itself, only returns suggestions the creator adds one at a time through the normal `POST /api/experiences` endpoint. No Sprint Journal draft this time (resumes don't produce narrative content the way a README does). Rate-limited the same way as Milestone 12's sanitization cap: `ResumeImportLog` (new entity, new `resume_import_logs` table) caps a tenant at 5 resume imports/hour — logged after the LLM call regardless of whether parsing succeeded, since the cost was incurred either way. A scanned/image-only PDF (no extractable text layer) still stores the file and updates `CvFileUrl`, but returns a message instead of suggestions rather than erroring.

### Step 48 — new migration (ResumeImportLog)

```bash
cd Devlog.os/api/DevLog.Api
dotnet ef migrations add AddResumeImportLog --project ../DevLog.Infrastructure --startup-project .
dotnet ef database update --project ../DevLog.Infrastructure --startup-project .
```

### Step 49 — apply the new RLS policy

```bash
psql -h localhost -U devlog -d devlog -f ../sql/009_resume_import_logs_policy.sql
```
(password: `devlog_local`, from `docker-compose.yml`)

### Step 50 — nothing new to configure, just rebuild and restart

`appsettings.Development.json` already has a `Storage` section (`LocalDiskPath: "uploads"`, `PublicBaseUrl: "http://localhost:61726/uploads"`) — adjust `PublicBaseUrl`'s port if your API runs on a different one.

```bash
dotnet build
dotnet run
```
```bash
cd Devlog.os/web
npm run dev
```

### Step 51 — smoke test

1. `/dashboard/import` → scroll to "Import from Resume" → upload a real PDF resume → Upload.
2. Confirm a "Resume saved as your CV download" link appears and opens the actual file.
3. Confirm suggestion cards render (type, title, an anonymized company label — **check this is never your real employer's name**, dates, technologies, key achievement) with an "Add to Experience" button.
4. Click "Add to Experience" on one → confirm it appears on `/dashboard/experience`, and that Ask AI can answer a question about it afterward.
5. Upload the same resume 6 times in an hour → confirm the 6th attempt returns the 429 rate-limit message instead of a suggestion (still stores the file each time).
6. Try a non-PDF file (rename a `.txt` to `.pdf`) → confirm a clear 400 error, not a crash.

### Handoff — commit and push

```bash
git add .
git commit -m "Add Milestone 13b: resume upload with AI-drafted suggestions + CV hosting"
git push
```

---

## Milestone 14 — paused visibility state (reviewed, already built)

No work shipped here — checked the actual code before starting, rather than trusting the roadmap's "⬜ Not started" tag. `Paused` has been enforceable since Sprint 0 (it's always been in the `PortfolioVisibility` enum), `sql/004`'s RLS `public_read` policy already excludes it at the database layer, all four anonymous read paths (`PortfolioController.GetPublicPortfolio`, `AskAiService`, `InterviewRequestService.CreateAsync`, `SprintJournalController`) already re-check it explicitly, and Milestone 11's dashboard already ships a working `<select>` control for it (Profile → Visibility → "Paused — hidden from everyone, including your own link"). Roadmap corrected in place. Nothing to smoke-test that isn't already covered by the existing dashboard.

## Bugfix — Sprint Journal public pages were never wired off mock data

Found via Chika publishing a real entry and not being able to find it on his own `/journal` page. `[slug]/journal/page.tsx` and `[slug]/journal/[entryId]/page.tsx` were still importing hardcoded fixture data from `mock-journal.ts` — Milestone 11's "wire public pages off mock data" pass only actually fixed `mock-portfolio.ts` (which Home and Experience depend on), never `mock-journal.ts`. Fixed the same way: real `fetch()` calls against `GET /api/portfolio/:slug/entries[/:id]`, same filename and function signatures, zero caller changes. No migration, no backend change — just restart the frontend dev server (or it'll pick up the change automatically under `npm run dev`) and refresh `/{slug}/journal`.

## Milestone 16 — creator analytics dashboard (built ahead of Milestone 15)

Built out of roadmap order on purpose: Milestone 15 (Discover) names its own precondition in the roadmap — "needs enough public tenants to be useful" — which isn't true yet. Milestone 16's data (`portfolio_view_events`, `ai_query_logs`, `interview_requests`) has been logging since Milestone 5/12 with zero UI on top of it, so it was the higher-value thing to build right now. Confirmed with Chika via AskUserQuestion before starting rather than just picking one.

New `GET /api/analytics/summary` (`AnalyticsController`, creator-authenticated, read-only — no new tables, no new writes) returns lifetime + 7-day + 30-day totals for portfolio views and Ask AI questions, interview request counts by state (verified/pending/accepted), and a zero-filled daily series for the last 30 days of each. New `/dashboard/analytics` page renders it: three stat cards plus two plain-CSS bar charts (no chart library added — a handful of divs sized by percentage height, matching this project's existing all-Tailwind-utility-classes convention).

### Step 52 — nothing to apply, just rebuild and restart

```bash
cd Devlog.os/api/DevLog.Api
dotnet build
dotnet run
```
```bash
cd Devlog.os/web
npm run dev
```

### Step 53 — smoke test

1. `/dashboard/analytics` → confirm the three stat cards load with real numbers (views/queries should already be non-zero if you've been testing the public pages and Ask AI).
2. Load your public portfolio a couple of times, ask Ask AI a question, then reload `/dashboard/analytics` → confirm the totals and today's bar both went up.
3. Confirm the interview-request counts match what's on `/dashboard/interview-requests` (verified count should equal pending + accepted + declined-but-verified).

### Handoff — commit and push

```bash
git add .
git commit -m "Milestone 16: creator analytics dashboard; fix Sprint Journal mock-data bug; confirm Milestone 14 already complete"
git push
```

---

## What comes after this

Phase 1's two deferred security/instrumentation gaps are closed, and guided onboarding is now complete on both paths — GitHub import (Milestone 13) and resume upload (Milestone 13b) — removing the blank-page problem for a new developer signing up cold, from either starting point. Paused visibility (Milestone 14) turned out to already be built. The creator analytics dashboard (Milestone 16) is done, giving real visibility into views/queries/requests for the first time. Next up: Discover (Milestone 15, once there are enough public tenants for cross-candidate search to be worth building) and Learning Lab (Milestone 17). Billing/Stripe is explicitly deferred until there's an actual userbase worth charging — new developers get in free for now.
