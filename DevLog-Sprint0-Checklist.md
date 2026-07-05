# DevLog — Sprint 0 Kickoff Checklist

Execute this before writing any product feature. Exit criteria at the bottom — don't move to the next roadmap milestone (Sprint Journal + anonymization pipeline) until it's met.

**Explicitly out of scope for Sprint 0:** embeddings/pgvector, the LLM Gateway, Request Interview, Discover, billing, onboarding automation. All of that is later in the build order from the MVP roadmap. Sprint 0 is tenant isolation and auth, proven, and nothing else.

---

## 1. Environment setup

- [ ] .NET 8 SDK + Visual Studio 2022 (ASP.NET and web development workload)
- [ ] Docker Desktop — used for a local Postgres instance, not for deployment yet
- [ ] Node.js LTS — for the Next.js frontend, even though Sprint 0 is API-only; get it installed now
- [ ] Clerk dev application — sign up, create a test application, note the issuer URL. Chosen over Auth0 primarily on cost at DevLog's expected MAU volume (see architecture doc Section 4); this is what issues the JWTs the API validates
- [ ] **Spike first, before building anything else on top of it:** Clerk's ASP.NET Core JWT validation checks the `azp` (authorized party) claim rather than a conventional `aud` claim, and Clerk's .NET package ecosystem is newer than Auth0's — get a minimal `AddJwtBearer` setup validating a real Clerk-issued test token end-to-end before wiring the RLS interceptor or any real endpoints on top of it. If this spike turns up more friction than expected, that's the moment to reconsider, not after Step 5 is fully built
- [ ] Do **not** provision Azure Key Vault yet. For local development, use `dotnet user-secrets` for connection strings and API keys — Key Vault is for the deployed environment (Phase 1, but at deploy time, not now). Provisioning it before there's a repo to deploy is wasted setup.

**Local Postgres via Docker Compose** — create `docker-compose.yml` at the repo root:

```yaml
services:
  postgres:
    image: pgvector/pgvector:pg16
    environment:
      POSTGRES_USER: devlog
      POSTGRES_PASSWORD: devlog_local
      POSTGRES_DB: devlog
    ports:
      - "5432:5432"
    volumes:
      - devlog_pg_data:/var/lib/postgresql/data
volumes:
  devlog_pg_data:
```

Using the `pgvector/pgvector` image now (even though embeddings aren't built until later) avoids a second migration to add the extension mid-project — the extension ships pre-installed, you just won't `CREATE EXTENSION vector` or use it until the RAG pipeline milestone.

---

## 2. Repo and solution structure

- [ ] `git init`, with a `.gitignore` covering both .NET (`bin/`, `obj/`) and Node (`node_modules/`, `.next/`)
- [ ] Create the solution: `dotnet new sln -n DevLog`
- [ ] Projects:
  - `DevLog.Domain` — entity classes and enums only, no dependencies on EF Core or ASP.NET
  - `DevLog.Infrastructure` — `DbContext`, EF Core migrations, the RLS connection interceptor
  - `DevLog.Api` — ASP.NET Core Web API, references Domain + Infrastructure
  - `DevLog.Tests` — the isolation proof test lives here
- [ ] `devlog-web/` — a separate Next.js app (`npx create-next-app@latest`), can be its own repo or a sibling folder; nothing to build in it yet beyond confirming it runs

---

## 3. First migration: Phase 1 schema subset only

Model only what Phase 1 needs — skip `embeddings`, `interview_requests`, `portfolio_view_events`, `entry_confirmations`, `resume_imports`, `admin_audit_log` until their features are actually being built.

- [ ] `Tenant` (id, user_id, slug, plan_tier, created_at)
- [ ] `Portfolio` (tenant_id, headline, current_role, years_experience, core_technologies jsonb, linkedin_url, github_url, cv_file_url, visibility, discoverable)
- [ ] `Experience` (id, tenant_id, experience_type, company_label, duration, technologies jsonb, key_achievement)
- [ ] `RawEntry` (id, tenant_id, entry_type, raw_content, status, created_at)
- [ ] `PublishedEntry` (id, raw_entry_id, tenant_id, sanitized_content, tags jsonb, published_at)
- [ ] `SanitizationAudit` (id, published_entry_id, model_version, diff, approved_by, approved_at)
- [ ] `Subscription` (tenant_id, plan, ai_model_tier, sanitization_quota) — just enough to track quota; no Stripe integration yet
- [ ] Configure `Npgsql.EntityFrameworkCore.PostgreSQL` in `DevLog.Infrastructure`
- [ ] `dotnet ef migrations add InitialSchema`, apply it against the Docker Postgres instance

---

## 4. RLS: the one piece everything else depends on

- [ ] In the migration (or a raw-SQL follow-up migration), enable RLS and add the tenant isolation policy on **both** `raw_entries` and `published_entries` (Section 9 of the architecture doc flagged that the original write-up only specified this for `raw_entries` — do both from the start here):

```sql
ALTER TABLE raw_entries ENABLE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON raw_entries
  USING (tenant_id = current_setting('app.current_tenant')::uuid);

ALTER TABLE published_entries ENABLE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON published_entries
  USING (tenant_id = current_setting('app.current_tenant')::uuid);
```

- [ ] Implement the connection interceptor in `DevLog.Infrastructure` (a `DbConnectionInterceptor` override on connection open, or a `SaveChangesInterceptor` paired with a scoped "current tenant" accessor) that runs `SET app.current_tenant = @tenantId` as the first command on every connection checkout within a request scope
- [ ] Register the interceptor once in `DevLog.Api`'s DI setup

**Proof test (in `DevLog.Tests`) — do not skip this:**
- [ ] Seed two fake tenants, each with a `RawEntry`
- [ ] Query as tenant A through the normal `DbContext`, deliberately omit any `WHERE tenant_id = ...` clause in the test query itself
- [ ] Assert tenant B's row is never returned
- [ ] This test is the actual exit criterion below, not a nice-to-have

---

## 5. Auth wiring

- [ ] Configure JWT bearer middleware in `DevLog.Api`'s `Program.cs` (`AddJwtBearer`) pointed at the Clerk dev application's issuer, validating the `azp` claim per the spike in Step 1
- [ ] Define the three roles from the architecture doc (Creator, Recruiter, Admin) as claims the JWT carries, plus `tenant_id` for creators
- [ ] Confirm the interceptor from Step 4 reads `tenant_id` from the **verified claim**, not from any request parameter
- [ ] Manual check: obtain a real test JWT from the dev tenant, call a simple authenticated endpoint (even just `GET /portfolio/me`), confirm the returned data is scoped to the calling tenant

---

## Exit criteria — don't move on until all of these are true

- [ ] The isolation proof test in Step 4 passes
- [ ] A real JWT from the dev identity provider authenticates against the API and its claims correctly set the RLS session variable
- [ ] `dotnet ef database update` runs clean against the Dockerized Postgres from a fresh clone
- [ ] The Next.js app builds and runs, even with no real pages yet

Once these hold, the next milestone per the MVP roadmap is Sprint Journal entry creation plus the anonymization pipeline — that's where `raw_entries` actually starts getting written to and the LLM Gateway gets built for the first time.
