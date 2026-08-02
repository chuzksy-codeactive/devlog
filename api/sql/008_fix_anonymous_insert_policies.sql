-- Run this AFTER 007 has been applied, same pattern as prior scripts.
--
-- Real bug, caught by Chika testing Milestone 12: portfolio_view_events
-- rows were never actually landing, despite loading the public portfolio
-- page repeatedly. Root cause: sql/007 only gave portfolio_view_events a
-- bare `tenant_isolation` policy (no FOR clause, no explicit WITH CHECK).
-- Per Postgres's own rule (already learned the hard way once this build,
-- in TenantProvisioningService's auto-provisioning bug), a policy with no
-- explicit WITH CHECK falls back to using its USING expression for INSERT
-- too. GetPublicPortfolio is anonymous — no real app.current_tenant is
-- ever set on that connection — so every INSERT attempt evaluated
-- `tenant_id = current_setting(...)::uuid` against a value that could
-- never match a real tenant id, and Postgres silently rejected the row.
-- PortfolioController's `catch { }` around the insert swallowed the
-- resulting exception with no logging at all, so this failed completely
-- silently instead of erroring loudly.
--
-- Same exact gap existed in ai_query_logs since Milestone 4 (sql/002) —
-- caught while fixing this one, not separately reported. AskAiService's
-- insert isn't wrapped in a try/catch at all, so every Ask AI question
-- could have been throwing a 500 instead of returning an answer, for the
-- same underlying reason. Both get the missing policy here.
--
-- Apply with: psql -h localhost -U devlog -d devlog -f sql/008_fix_anonymous_insert_policies.sql
-- (password: devlog_local, from docker-compose.yml)

-- Same shape as 003's public_insert on interview_requests: any anonymous
-- caller may INSERT a view/query-log row for a tenant whose portfolio is
-- actually publicly visible. These are write-only from the anonymous
-- side — neither table has (or should have) a public_read policy, so this
-- doesn't open up any new read access, only lets the log rows land.
CREATE POLICY public_insert ON portfolio_view_events
  FOR INSERT
  WITH CHECK (
    EXISTS (
      SELECT 1 FROM portfolios p
      WHERE p.tenant_id = portfolio_view_events.tenant_id
        AND p.visibility <> 'Paused'
    )
  );

CREATE POLICY public_insert ON ai_query_logs
  FOR INSERT
  WITH CHECK (
    EXISTS (
      SELECT 1 FROM portfolios p
      WHERE p.tenant_id = ai_query_logs.tenant_id
        AND p.visibility <> 'Paused'
    )
  );
