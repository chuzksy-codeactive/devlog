
ALTER TABLE experiences ENABLE ROW LEVEL SECURITY;

CREATE POLICY tenant_isolation ON experiences
  USING (tenant_id = current_setting('app.current_tenant', true)::uuid);

CREATE POLICY public_read ON experiences
  FOR SELECT
  USING (
    EXISTS (
      SELECT 1 FROM portfolios p
      WHERE p.tenant_id = experiences.tenant_id
        AND p.visibility <> 'Paused'
    )
  );

