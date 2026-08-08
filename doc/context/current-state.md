# current-state.md
## Done
- PR #6 merged: Application Insights wiring completed, with a single telemetry pipeline (`AddOpenTelemetry().UseAzureMonitor(...)`) to avoid duplicate ingestion.
- PR #7 merged: deploy workflow smoke test fixed to use deployed staging slot URL output instead of a hardcoded hostname.
- Deploy workflow trigger on `push` to `main` is confirmed working.
- Azure OIDC staging authentication issue fixed (federated credential subject aligned to GitHub environment token format).
- Secret-scan regression from `api/appsettings.Development.json` was removed from PR history and ignore rules were hardened for environment-specific appsettings files.
- Observability implementation runbook added to `DevLog-Deployment-Checklist.md` with concrete alert definitions (5xx rate, p95 latency, exception spikes), Action Group wiring, and dashboard follow-up.

## In progress
- Phase 5 observability is partially complete: telemetry export is wired and alert setup steps are documented; Azure alert rules are still pending creation.
- Phase 6 end-to-end staging smoke tests are still pending beyond `/health`.

## Blockers
- No hard blocker currently for CI/CD deploy flow.
- Remaining progress depends on Azure portal configuration work (alert rules) and manual staging validation runs.