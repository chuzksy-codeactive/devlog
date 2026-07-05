# DevLog

**A developer career operating system.**

Most developer portfolios show what you know — a skills list, a CV, a GitHub link. DevLog shows how you work. Instead of a static About page, developers publish sprint-level case studies — real problems, real trade-offs, real technical decisions — anonymized so no employer confidentiality is at risk. Recruiters browse a candidate's Experience timeline and Sprint Journal, and can ask an embedded AI assistant direct questions about real experience, backed by evidence instead of keywords.

## Status

Sprint 0 complete: multi-tenant schema, Row-Level Security isolation (proven with a dedicated test), and Clerk authentication wired end to end. See `DevLog-Build-Journal.md` for the running story of how this is being built, and `GETTING-STARTED.md` for exact setup commands.

## Stack

- **API** — ASP.NET Core (C#), EF Core + Npgsql, PostgreSQL with `pgvector`
- **Auth** — Clerk
- **Frontend** — Next.js (TypeScript, App Router, Tailwind)
- **Local dev** — Docker Compose (Postgres)

## Repo structure

```
api/    — ASP.NET Core solution (DevLog.Domain, DevLog.Infrastructure, DevLog.Api, DevLog.Tests)
web/    — Next.js frontend
```

## Getting started

See `GETTING-STARTED.md` for the full setup walkthrough (local Postgres, first migration, RLS policies, Clerk configuration, running the tests).

## Planning docs

- `DevLog-Product-Brief-v2.md` — positioning, information architecture, feature rationale
- `DevLog-Technical-Architecture.md` — full architecture: schema, RAG pipeline, anonymization, auth, security, stack
- `DevLog-MVP-Roadmap.md` — phased build plan (MVP → Growth → Scale)
- `DevLog-Sprint0-Checklist.md` — the kickoff checklist this repo was built from
