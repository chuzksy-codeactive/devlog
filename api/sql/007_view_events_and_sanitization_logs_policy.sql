
ALTER TABLE portfolio_view_events ENABLE ROW LEVEL SECURITY;

CREATE POLICY tenant_isolation ON portfolio_view_events
  USING (tenant_id = current_setting('app.current_tenant', true)::uuid);

ALTER TABLE sanitization_run_logs ENABLE ROW LEVEL SECURITY;

CREATE POLICY tenant_isolation ON sanitization_run_logs
  USING (tenant_id = current_setting('app.current_tenant', true)::uuid);

