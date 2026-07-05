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

## What comes after this

Once Steps 1–7 all check out, the next milestone per `DevLog-MVP-Roadmap.md` is Sprint Journal entry creation plus the anonymization pipeline — that's where `raw_entries` starts getting written to for real and the LLM Gateway gets built for the first time. Come back and I'll help design and write that next.
