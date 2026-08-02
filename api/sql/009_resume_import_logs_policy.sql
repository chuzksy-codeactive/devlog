-- Run this AFTER the migration adding resume_import_logs has been applied
-- (`dotnet ef database update`), same pattern as 001-008.
--
-- Milestone 13b — resume upload + AI extraction's cost-abuse control.
-- Internal-only, same shape as 007's sanitization_run_logs: no public_read
-- policy (never read by a public API route) and no public_insert policy
-- either, unlike 008's fix — resume import is always an authenticated,
-- [Authorize]-gated creator action (OnboardingController), never called
-- anonymously the way portfolio_view_events/ai_query_logs are, so there's
-- no anonymous-insert gap to guard against here.
--
-- Apply with: psql -h localhost -U devlog -d devlog -f sql/009_resume_import_logs_policy.sql
-- (password: devlog_local, from docker-compose.yml)

ALTER TABLE resume_import_logs ENABLE ROW LEVEL SECURITY;

CREATE POLICY tenant_isolation ON resume_import_logs
  USING (tenant_id = current_setting('app.current_tenant', true)::uuid);

-- Same production note as every prior RLS script: the app currently
-- connects as devlog_app (Milestone 8's fix), a non-superuser/non-owner
-- role, so this policy applies to real traffic already — no FORCE ROW
-- LEVEL SECURITY needed, same as everything since sql/005.
