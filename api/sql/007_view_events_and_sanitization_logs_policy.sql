-- Run this AFTER the migration adding portfolio_view_events and
-- sanitization_run_logs has been applied (`dotnet ef database update`),
-- same pattern as 001-006.
--
-- Milestone 12 — closes both Phase 1 gaps flagged (and deferred) in the MVP
-- roadmap review: portfolio_view_events was never populated at all, and
-- Subscription.SanitizationQuota existed with nothing enforcing it.
--
-- Both tables are internal-only, same shape as 002's ai_query_logs: no
-- public_read policy, deliberately — neither is ever read by a public API
-- route, only counted against by PortfolioController/SanitizationPipelineService
-- for their own tenant's rows.
--
-- Apply with: psql -h localhost -U devlog -d devlog -f sql/007_view_events_and_sanitization_logs_policy.sql
-- (password: devlog_local, from docker-compose.yml)

ALTER TABLE portfolio_view_events ENABLE ROW LEVEL SECURITY;

CREATE POLICY tenant_isolation ON portfolio_view_events
  USING (tenant_id = current_setting('app.current_tenant', true)::uuid);

ALTER TABLE sanitization_run_logs ENABLE ROW LEVEL SECURITY;

CREATE POLICY tenant_isolation ON sanitization_run_logs
  USING (tenant_id = current_setting('app.current_tenant', true)::uuid);

-- Same production note as every prior RLS script: the app currently
-- connects as devlog_app (Milestone 8's fix), a non-superuser/non-owner
-- role, so these policies apply to real traffic already — no FORCE ROW
-- LEVEL SECURITY needed, same as everything since sql/005.
