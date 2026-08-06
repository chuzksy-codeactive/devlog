
ALTER TABLE interview_requests ENABLE ROW LEVEL SECURITY;

CREATE POLICY tenant_isolation ON interview_requests
  USING (tenant_id = current_setting('app.current_tenant', true)::uuid);

CREATE POLICY public_insert ON interview_requests
  FOR INSERT
  WITH CHECK (
    EXISTS (
      SELECT 1 FROM portfolios p
      WHERE p.tenant_id = interview_requests.tenant_id
        AND p.visibility <> 'Paused'
    )
  );

