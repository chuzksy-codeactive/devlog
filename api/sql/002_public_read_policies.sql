-- Run this AFTER the AddEmbeddingsAndAiQueryLogs migration has been applied
-- (`dotnet ef database update`), same pattern as 001.
--
-- Closes the gap 001 explicitly flagged: "published_entries needs [a public
-- policy] too, even though it's meant to be public-read eventually — that
-- will be a second, explicit policy for the anon role once the public
-- browsing endpoint is built." Ask AI (this milestone) is that endpoint —
-- it reads published_entries/embeddings across arbitrary tenants for an
-- anonymous recruiter, so app.current_tenant is never set on that
-- connection. Without the policies below, RLS would silently return zero
-- rows for every Ask AI query.
--
-- Postgres combines multiple PERMISSIVE policies on the same command with
-- OR, so each table below ends up with: "you're the owning tenant" OR
-- "the portfolio is publicly visible" — either grants SELECT.
--
-- Apply with: psql -h localhost -U devlog -d devlog -f sql/002_public_read_policies.sql
-- (password: devlog_local, from docker-compose.yml)

CREATE POLICY public_read ON published_entries
  FOR SELECT
  USING (
    EXISTS (
      SELECT 1 FROM portfolios p
      WHERE p.tenant_id = published_entries.tenant_id
        AND p.visibility <> 'Paused'
    )
  );

ALTER TABLE embeddings ENABLE ROW LEVEL SECURITY;

CREATE POLICY tenant_isolation ON embeddings
  USING (tenant_id = current_setting('app.current_tenant', true)::uuid);

CREATE POLICY public_read ON embeddings
  FOR SELECT
  USING (
    EXISTS (
      SELECT 1 FROM portfolios p
      WHERE p.tenant_id = embeddings.tenant_id
        AND p.visibility <> 'Paused'
    )
  );

-- ai_query_logs: no public_read policy, deliberately — this is an internal
-- audit/rate-limit table, never read by any public API route, same
-- deny-all-by-default shape as raw_entries in 001.
ALTER TABLE ai_query_logs ENABLE ROW LEVEL SECURITY;

CREATE POLICY tenant_isolation ON ai_query_logs
  USING (tenant_id = current_setting('app.current_tenant', true)::uuid);

-- Production note, not yet acted on: in this local setup the app connects
-- as the tables' owning role, and Postgres table owners bypass RLS by
-- default (FORCE ROW LEVEL SECURITY is not set anywhere in this schema).
-- The existing tenant-isolation proof test still passes because it's
-- exercising the isolation *policy logic*, not owner-vs-non-owner
-- behavior. Before real multi-tenant traffic, this needs a non-owner
-- application role plus FORCE ROW LEVEL SECURITY on every RLS table, so
-- the database-level guarantee holds even against the app's own
-- connection, not just against a hypothetical other role. Tracked here
-- rather than silently assumed away.
