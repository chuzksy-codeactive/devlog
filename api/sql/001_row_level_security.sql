
ALTER TABLE raw_entries ENABLE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON raw_entries
  USING (tenant_id = current_setting('app.current_tenant', true)::uuid);

ALTER TABLE published_entries ENABLE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON published_entries
  USING (tenant_id = current_setting('app.current_tenant', true)::uuid);

