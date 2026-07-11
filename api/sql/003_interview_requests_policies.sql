-- Run this AFTER the AddInterviewRequests migration has been applied
-- (`dotnet ef database update`), same pattern as 001/002.
--
-- interview_requests needs both halves at once, unlike published_entries/
-- embeddings in 002, which only ever needed public SELECT: an anonymous
-- recruiter must be able to INSERT a new request (no JWT, no
-- app.current_tenant set on that connection — see InterviewRequestsController,
-- deliberately not [Authorize]), and the owning creator must be able to
-- SELECT/UPDATE their own tenant's requests (inbox, accept, decline) via
-- the normal authenticated path. Both policies are PERMISSIVE, so Postgres
-- combines them with OR per command, same as 002's public_read pattern.
--
-- Apply with: psql -h localhost -U devlog -d devlog -f sql/003_interview_requests_policies.sql
-- (password: devlog_local, from docker-compose.yml)

ALTER TABLE interview_requests ENABLE ROW LEVEL SECURITY;

-- Creator's own tenant: full access (SELECT the inbox, UPDATE on
-- accept/decline). No FOR clause -> Postgres applies the USING expression
-- to every command, and falls back to USING as the WITH CHECK expression
-- too since none is given here — same shape as 001's tenant_isolation
-- policies.
CREATE POLICY tenant_isolation ON interview_requests
  USING (tenant_id = current_setting('app.current_tenant', true)::uuid);

-- Anonymous recruiter submission: no app.current_tenant is set on this
-- connection at all, so this is the only policy that can grant the INSERT.
-- Same visibility guard as 002's public_read — a Paused portfolio refuses
-- interview requests too, not just AI questions and entry reads.
CREATE POLICY public_insert ON interview_requests
  FOR INSERT
  WITH CHECK (
    EXISTS (
      SELECT 1 FROM portfolios p
      WHERE p.tenant_id = interview_requests.tenant_id
        AND p.visibility <> 'Paused'
    )
  );

-- Production note (same as 002, not repeated per-file elsewhere): the app
-- currently connects as the tables' owning role, which bypasses RLS by
-- default. Needs a non-owner application role + FORCE ROW LEVEL SECURITY
-- before real traffic — tracked in 002's comment, applies here too.
