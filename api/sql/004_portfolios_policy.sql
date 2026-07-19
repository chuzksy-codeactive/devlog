-- Run this AFTER the migration adding/altering the relevant columns has
-- been applied (`dotnet ef database update`), same pattern as 001/002/003.
--
-- portfolios never got an RLS policy in 001-003 — a real gap, not a
-- deliberate deferral. PortfolioController.GetMyPortfolio relies on "no
-- WHERE clause, RLS enforces scope" (same pattern as EntriesController),
-- but with zero RLS policies on this table that comment was simply false:
-- the query returned whichever row sorted first, from ANY tenant. Invisible
-- with at most one row in a dev database; live the moment a second tenant
-- exists, which real onboarding (Milestone 7) now makes routine. Fixing it
-- here, not deferring it.
--
-- Apply with: psql -h localhost -U devlog -d devlog -f sql/004_portfolios_policy.sql
-- (password: devlog_local, from docker-compose.yml)

ALTER TABLE portfolios ENABLE ROW LEVEL SECURITY;

CREATE POLICY tenant_isolation ON portfolios
  USING (tenant_id = current_setting('app.current_tenant', true)::uuid);

-- Same public_read shape as 002's published_entries/embeddings policies —
-- Ask AI and Request Interview both look up a portfolio by slug for an
-- anonymous caller (no app.current_tenant set at all), and would otherwise
-- see zero rows now that RLS is actually enabled here.
CREATE POLICY public_read ON portfolios
  FOR SELECT
  USING (visibility <> 'Paused');

-- Deliberately NOT adding RLS to the tenants table in this pass, unlike
-- portfolios. Two reasons: (1) nothing on a Tenant row is actually
-- sensitive (id, a Clerk user id, a public slug, a plan tier, a
-- timestamp) — the data worth protecting lives in raw_entries/
-- published_entries/embeddings/interview_requests, which already have
-- real RLS; (2) TenantProvisioningService's own lookup-by-Clerk-user-id
-- query runs BEFORE app.current_tenant is known for that request (that's
-- the whole point of the lookup) — RLS-protecting tenants would create a
-- chicken-and-egg problem where the provisioning service could never find
-- a caller's own row on their second-and-later requests. AskAiService and
-- InterviewRequestService also join through tenants (via Portfolio.Tenant)
-- for anonymous slug lookups, which a tenant_isolation-only policy would
-- break outright. Revisit if a genuinely sensitive column ever gets added
-- to Tenant.

-- Production note (same as 002/003, not repeated per-file elsewhere): the
-- app currently connects as the tables' owning role, which bypasses RLS by
-- default. Needs a non-owner application role + FORCE ROW LEVEL SECURITY
-- before real traffic.
