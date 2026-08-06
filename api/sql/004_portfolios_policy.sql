
ALTER TABLE portfolios ENABLE ROW LEVEL SECURITY;

CREATE POLICY tenant_isolation ON portfolios
  USING (tenant_id = current_setting('app.current_tenant', true)::uuid);

CREATE POLICY public_read ON portfolios
  FOR SELECT
  USING (visibility <> 'Paused');

