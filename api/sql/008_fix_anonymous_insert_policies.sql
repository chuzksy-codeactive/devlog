
CREATE POLICY public_insert ON portfolio_view_events
  FOR INSERT
  WITH CHECK (
    EXISTS (
      SELECT 1 FROM portfolios p
      WHERE p.tenant_id = portfolio_view_events.tenant_id
        AND p.visibility <> 'Paused'
    )
  );

CREATE POLICY public_insert ON ai_query_logs
  FOR INSERT
  WITH CHECK (
    EXISTS (
      SELECT 1 FROM portfolios p
      WHERE p.tenant_id = ai_query_logs.tenant_id
        AND p.visibility <> 'Paused'
    )
  );
