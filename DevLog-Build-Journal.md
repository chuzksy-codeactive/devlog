# Building DevLog — running journal

Kept in the exact shape of a DevLog Sprint Journal entry (Problem / Solution / Technical Design / Challenges / Result), since the plan is to eventually publish "building DevLog" as a real entry on Chika's own DevLog portfolio once it launches — using the product to document its own creation. This file gets updated as each feature ships; treat it as a draft to be sanitized and tightened before actual publication, not final copy.

---

## Title (draft)

Building DevLog: a developer career operating system, from architecture review to first working auth.

## Delivered so far

Sprint 0: multi-tenant schema, row-level security, and Clerk authentication, end to end and proven.

## Problem

Standard developer portfolios (a CV, a skills list, a GitHub link) show *what* a developer knows but not *how* they work — the exact evidence a recruiter needs to decide whether to book an interview. The idea: a "developer career operating system" where sprint-level case studies and an AI assistant let recruiters query real technical decisions directly, anonymized so no employer confidentiality is at risk.

## Solution

A multi-tenant SaaS (ASP.NET Core + Postgres + Clerk + Next.js) where each developer gets a portfolio with an Experience timeline, a Sprint Journal of case studies, a Learning Lab, and a persistent AI assistant recruiters can query — all sitting on a write-time anonymization pipeline that sanitizes and requires human approval before anything is public.

## Technical design

- **Tenant isolation enforced twice**: application middleware sets tenant context from a verified Clerk JWT claim, and Postgres Row-Level Security independently refuses cross-tenant rows even if application code has a bug — proven with a test that queries deliberately without a `WHERE tenant_id` clause and confirms RLS alone blocks the leak.
- **Raw and sanitized content are physically separate tables**, not a status flag, so there is no code path — buggy or not — that can expose an unsanitized draft.
- **Clerk over Auth0** for authentication, chosen primarily on cost at the expected mostly-free-tier user volume, accepting a less conventional JWT validation pattern (`azp` claim instead of `aud`) as the trade-off.
- **Snake_case database columns** via EF Core's naming-convention package, so the C# entity model and the raw SQL (RLS policies, future analytics queries) stay in the same vocabulary rather than fighting a PascalCase/snake_case mismatch everywhere.

## Challenges

- **The RLS policy and the EF Core schema disagreed on column casing.** EF Core defaults to PascalCase column names (`TenantId`); the RLS SQL was written assuming Postgres convention (`tenant_id`). First attempt to apply the RLS script failed with `column "tenant_id" does not exist`. Fixed at the root — added `EFCore.NamingConventions` and `UseSnakeCaseNamingConvention()` — rather than patching the SQL to match PascalCase, since every future hand-written SQL snippet would have hit the same mismatch otherwise.
- **The JWT spike caught exactly what it was designed to catch.** The first real Clerk token came back with an `azp` claim of Clerk's own dashboard origin, not the application's — because `window.Clerk.session.getToken()` was run from the Clerk Dashboard page itself rather than the application's hosted Account Portal sign-in page. Once corrected, the token's `azp` still didn't match the assumed `http://localhost:3000` (that assumption was for testing via a real frontend; testing via Clerk's hosted Account Portal produces a different origin) — config adjusted to match what was actually being tested with.
- **PowerShell's `curl` is not curl.** It's aliased to `Invoke-WebRequest`, which doesn't accept curl's `-H` flag at all, producing a parameter-binding error that looked unrelated to auth. Fixed by using `curl.exe` explicitly to reach the real binary.

## Result

TBD — fill in once the product is live and there's a real outcome to report (interview requests generated, recruiter engagement, etc.), per the instrumentation plan already in the MVP roadmap.
