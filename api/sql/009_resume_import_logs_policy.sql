
ALTER TABLE resume_import_logs ENABLE ROW LEVEL SECURITY;

CREATE POLICY tenant_isolation ON resume_import_logs
  USING (tenant_id = current_setting('app.current_tenant', true)::uuid);

