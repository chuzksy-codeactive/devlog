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

## What comes after this

Once Step 18 checks out, the next milestone per `DevLog-MVP-Roadmap.md` is Request Interview — the last Phase 1 feature and the product's actual conversion event. Come back and I'll help design and write that next.
