
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

ALTER TABLE ai_query_logs ENABLE ROW LEVEL SECURITY;

CREATE POLICY tenant_isolation ON ai_query_logs
  USING (tenant_id = current_setting('app.current_tenant', true)::uuid);

