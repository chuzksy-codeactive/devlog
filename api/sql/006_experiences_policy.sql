-- Run this AFTER the migration adding Experience.Title has been applied
-- (`dotnet ef database update`), same pattern as 001-004.
--
-- experiences never got an RLS policy in 001-005 at all — not "only
-- missing public_read" like portfolios was in Milestone 7, genuinely zero
-- policies, meaning RLS was never even enabled on this table. Invisible
-- until now because nothing ever queried it through the app (Home/
-- Experience pages ran on mock data since Milestone 3, and no
-- creator-facing CRUD endpoint existed either) — but it was a real,
-- latent cross-tenant hole the moment either showed up. Milestone 11
-- adds both at once (ExperienceController + the public portfolio
-- endpoint), so this can't stay deferred any longer.
--
-- Apply with: psql -h localhost -U devlog -d devlog -f sql/006_experiences_policy.sql
-- (password: devlog_local, from docker-compose.yml)

ALTER TABLE experiences ENABLE ROW LEVEL SECURITY;

-- Creator's own tenant: full access (ExperienceController's list/create/
-- update/delete, all scoped by "no WHERE tenant_id clause, RLS enforces
-- it" — same pattern as EntriesController).
CREATE POLICY tenant_isolation ON experiences
  USING (tenant_id = current_setting('app.current_tenant', true)::uuid);

-- Anonymous public read: GET /api/portfolio/{slug} (PortfolioController)
-- needs to read another tenant's experiences with no app.current_tenant
-- set at all. experiences has no visibility column of its own — visibility
-- lives on portfolios, keyed by tenant_id — so this checks it via EXISTS,
-- same cross-table shape as 003's public_insert policy on
-- interview_requests.
CREATE POLICY public_read ON experiences
  FOR SELECT
  USING (
    EXISTS (
      SELECT 1 FROM portfolios p
      WHERE p.tenant_id = experiences.tenant_id
        AND p.visibility <> 'Paused'
    )
  );

-- Production note (same as 002/003/004, not repeated per-file elsewhere):
-- the app currently connects as the tables' owning role for migrations
-- only as of Milestone 8's fix — real traffic uses the restricted
-- devlog_app role (sql/005), so this policy is actually enforced, not
-- just internally consistent SQL.
