# Building DevLog — running journal

Kept in the exact shape of a DevLog Sprint Journal entry (Problem / Solution / Technical Design / Challenges / Result), since the plan is to eventually publish "building DevLog" as a real entry on Chika's own DevLog portfolio once it launches — using the product to document its own creation. This file gets updated as each feature ships; treat it as a draft to be sanitized and tightened before actual publication, not final copy.

---

## Title (draft)

Building DevLog: a developer career operating system, from architecture review to first working auth.

## Delivered so far

Sprint 0: multi-tenant schema, row-level security, and Clerk authentication, end to end and proven. Milestone 2: Sprint Journal entry creation plus the write-time anonymization pipeline, backed by Anthropic. Milestone 3: the first real UI — Home and Experience pages.

## Problem

Standard developer portfolios (a CV, a skills list, a GitHub link) show *what* a developer knows but not *how* they work — the exact evidence a recruiter needs to decide whether to book an interview. The idea: a "developer career operating system" where sprint-level case studies and an AI assistant let recruiters query real technical decisions directly, anonymized so no employer confidentiality is at risk.

## Solution

A multi-tenant SaaS (ASP.NET Core + Postgres + Clerk + Next.js) where each developer gets a portfolio with an Experience timeline, a Sprint Journal of case studies, a Learning Lab, and a persistent AI assistant recruiters can query — all sitting on a write-time anonymization pipeline that sanitizes and requires human approval before anything is public.

## Technical design

- **Tenant isolation enforced twice**: application middleware sets tenant context from a verified Clerk JWT claim, and Postgres Row-Level Security independently refuses cross-tenant rows even if application code has a bug — proven with a test that queries deliberately without a `WHERE tenant_id` clause and confirms RLS alone blocks the leak.
- **Raw and sanitized content are physically separate tables**, not a status flag, so there is no code path — buggy or not — that can expose an unsanitized draft.
- **Clerk over Auth0** for authentication, chosen primarily on cost at the expected mostly-free-tier user volume, accepting a less conventional JWT validation pattern (`azp` claim instead of `aud`) as the trade-off.
- **Snake_case database columns** via EF Core's naming-convention package, so the C# entity model and the raw SQL (RLS policies, future analytics queries) stay in the same vocabulary rather than fighting a PascalCase/snake_case mismatch everywhere.

- **The anonymization pipeline is the product's actual differentiator, so it got built before anything else on the roadmap** — `RawEntry` → LLM sanitization pass (Anthropic, model chosen for a structured find-and-replace-shaped task rather than open-ended generation) → creator review of a `SanitizedPreview` → approval writes a separate `PublishedEntry` row plus a `SanitizationAudit` (model version, a diff summary, approver, timestamp). Raw and published content stay in physically separate tables, same principle as Sprint 0's RLS design, just applied to content instead of tenant rows.
- **The sanitization prompt does two jobs at once**: strip employer-identifying details (using the creator's own `Experience.CompanyLabel` values as a known-terms blocklist so the model isn't guessing) and screen for prompt-injection-shaped content, since a sprint entry is later retrieved as RAG context for every recruiter who queries that candidate — a malicious instruction hidden in an entry would otherwise be replayed to every future reader. A regex safety net (emails, ticket IDs, internal hostnames) runs after the LLM pass as a deterministic backstop.
- **The sanitization audit trail deliberately doesn't store a raw-vs-sanitized text diff** — only a length delta plus model version/approver/timestamp. Storing the raw text anywhere outside `raw_entries`, even in a non-public audit table, would grow the exact leak surface the pipeline exists to shrink.
- **Synchronous for now, async later**: sanitization runs inline on the API request rather than through a background job queue, since Hangfire isn't wired up yet. The architecture doc's flowchart describes the async version — this is the same steps, just not yet decoupled from the request/response cycle.

- **Home and Experience shipped together, matching the split decided during design**: a recruiter's first-pass scan (Experience) and deep-dive (Sprint Journal) are different jobs, so the two pages stay visually distinct — Experience is a dense, dated timeline; Home is a hook. Built as a `/[slug]` dynamic route so the multi-tenant model reaches all the way to the URL, with a shared layout carrying the persistent nav, the floating Ask AI widget, and the Request Interview CTA across every page in the portfolio.
- **The "no job yet" state got built as a real path, not a fallback.** `currentRoleTitle` is nullable end to end — when null, Home renders "Actively building" instead of a title and simply omits the years-of-experience stat rather than showing "0 years." A second mock fixture (`new-grad-demo`) exists specifically to keep this state visible while building, instead of only ever looking at the fully-staffed demo data.
- **Years of experience is computed, not stored** — derived client-side from the earliest `Employer`-type experience's start date, deliberately excluding PersonalProject/OpenSource/Freelance/Education from that specific stat so it keeps its conventional meaning.
- **Ask AI and Request Interview are real, placed correctly, and honest about not working yet.** Both render in their final positions (Ask AI floating + embedded in the Home hero per the Product Brief; Request Interview next to it) but every interaction explains what's still missing (the embedding pipeline; the interview_requests backend) rather than faking a response — so the UI shape is real ahead of the pipelines that will drive it.
- **Frontend/backend built ahead of the wiring between them.** No API client exists yet — pages read from a mock data module (`lib/mock-portfolio.ts`) shaped like the real `GET /api/portfolio/:slug` response will be, so swapping mock for fetch later shouldn't ripple through the page components.

## Challenges

- **The RLS policy and the EF Core schema disagreed on column casing.** EF Core defaults to PascalCase column names (`TenantId`); the RLS SQL was written assuming Postgres convention (`tenant_id`). First attempt to apply the RLS script failed with `column "tenant_id" does not exist`. Fixed at the root — added `EFCore.NamingConventions` and `UseSnakeCaseNamingConvention()` — rather than patching the SQL to match PascalCase, since every future hand-written SQL snippet would have hit the same mismatch otherwise.
- **The JWT spike caught exactly what it was designed to catch.** The first real Clerk token came back with an `azp` claim of Clerk's own dashboard origin, not the application's — because `window.Clerk.session.getToken()` was run from the Clerk Dashboard page itself rather than the application's hosted Account Portal sign-in page. Once corrected, the token's `azp` still didn't match the assumed `http://localhost:3000` (that assumption was for testing via a real frontend; testing via Clerk's hosted Account Portal produces a different origin) — config adjusted to match what was actually being tested with.
- **`next build` couldn't be verified end-to-end in the build sandbox** — it tried to download the Linux SWC binary and the sandbox has no network access to `registry.npmjs.org`. Not a real bug: the mounted `node_modules` already has the Windows binary (`swc-win32-x64-msvc`) from Chika's own earlier `npm install`, so `npm run build` on his actual machine uses that directly. Verified what the sandbox could: `tsc --noEmit` and `eslint` both came back clean.
- **PowerShell's `curl` is not curl.** It's aliased to `Invoke-WebRequest`, which doesn't accept curl's `-H` flag at all, producing a parameter-binding error that looked unrelated to auth. Fixed by using `curl.exe` explicitly to reach the real binary.

## Result

TBD — fill in once the product is live and there's a real outcome to report (interview requests generated, recruiter engagement, etc.), per the instrumentation plan already in the MVP roadmap.
