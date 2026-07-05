-- Run this AFTER the initial EF Core migration has created the tables
-- (`dotnet ef database update`). EF Core has no native RLS API, so this
-- stays a plain SQL script rather than a migration — see architecture doc
-- Section 1 and the ".NET implementation note" there.
--
-- Apply with: psql -h localhost -U devlog -d devlog -f sql/001_row_level_security.sql
-- (password: devlog_local, from docker-compose.yml)

ALTER TABLE raw_entries ENABLE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON raw_entries
  USING (tenant_id = current_setting('app.current_tenant', true)::uuid);
-- No policy grants access to the anon/public role at all — raw_entries
-- must never be reachable from the public read path.

ALTER TABLE published_entries ENABLE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON published_entries
  USING (tenant_id = current_setting('app.current_tenant', true)::uuid);
-- Section 9 flagged that the original write-up only specified a policy for
-- raw_entries — published_entries needs one too, even though it's meant to
-- be public-read eventually (that will be a second, explicit policy for the
-- anon role once the public browsing endpoint is built; for Sprint 0, only
-- the owning tenant can read its own rows).

-- current_setting(..., true) — the second argument means "missing_ok":
-- if app.current_tenant was never set on this connection, this returns
-- NULL instead of raising an error, which is what the interceptor's
-- absence (e.g. a raw psql session) should safely resolve to: no access.
