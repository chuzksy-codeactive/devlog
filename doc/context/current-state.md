# current-state.md
## Done
- PR #6 merged: Application Insights wiring completed, with a single telemetry pipeline (`AddOpenTelemetry().UseAzureMonitor(...)`) to avoid duplicate ingestion.
- Cost-capped relaunch deploy flow is active: `.github/workflows/deploy.yml` now runs direct production deployment (`build-api` → `deploy-api-production` → `smoke-test-api` → `deploy-frontend`) against `app-devlog-api-prod-ca3c`, and the legacy swap workflow was removed.
- Deploy workflow trigger on `push` to `main` is confirmed working.
- Azure OIDC authentication is aligned to the `production` GitHub environment.
- Secret-scan regression from `api/appsettings.Development.json` was removed from PR history and ignore rules were hardened for environment-specific appsettings files.
- Observability implementation runbook added to `DevLog-Deployment-Checklist.md` with concrete alert definitions (5xx rate, p95 latency, exception spikes), Action Group wiring, and dashboard follow-up.

## In progress
- Phase 5 observability is partially complete: telemetry export is wired and alert setup steps are documented; Azure alert rules are still pending creation.
- End-to-end production smoke tests are still pending beyond `/health`.
- Frontend/API production auth+CORS issues were resolved; remaining work is observability alerts plus full smoke-test closure.
- Dashboard auth UX hardening completed in code: `/dashboard` now server-guards on Clerk auth and redirects signed-out users to `/`; home page auth state uses `SignedIn`/`SignedOut` plus explicit `afterSignOutUrl` to avoid stale "Go to dashboard" state after logout.

## Blockers
- No hard blocker currently for CI/CD deploy flow.
- Remaining progress depends on Azure portal configuration work (alert rules) and manual production validation runs.