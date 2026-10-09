# DevLog Production Deployment Checklist

Working checklist for taking DevLog from local dev to production on Azure. Ordered — each phase assumes the one before it is done. Check items off as you go; this file stays in the repo as the record of what's been done.

> **Update (cost-capped relaunch):** the live production pipeline is now direct production deploy (no staging slot swap). Use `.github/workflows/deploy.yml` as the source of truth: `build-api` → `deploy-api-production` → `smoke-test-api` → `deploy-frontend` against `app-devlog-api-prod-ca3c`.

---

## Phase 0 — Pre-launch fixes (do first, before touching infra)

- [x] Run `dotnet test` on `ValidationIntegrationTests` and confirm `CreateInterviewRequest_InvalidEmail_ReturnsValidationProblem` still passes. 934687b switched validation attributes from `[property: ...]` to `[param: ...]` on every DTO record — for classic MVC `[ApiController]` JSON-body binding, ASP.NET Core reads validation metadata off properties, not constructor parameters, so this may have silently disabled input validation. Revert to `[property:]` if the test fails.
- [x] Add a startup guard so non-Development environments require `Storage:Provider=AzureBlob`, mirroring the existing malware-scanner enforcement check. Right now the code default (`LocalDisk`) still mounts the old unprotected `UseStaticFiles` path with zero visibility check — a misconfigured deploy could silently reopen the CV-hosting hole Milestone 20 closed.
- [x] Remove the dead "CV file URL" field from `dashboard/profile/page.tsx` (backend stopped accepting `CvFileUrl` on `PUT /api/portfolio/me`; the field currently no-ops silently on save). Replace with a read-only "CV uploaded" indicator linking to `/dashboard/import`.

## Phase 1 — Secrets management (Azure Key Vault)

- [x] Create an Azure Key Vault for the production environment.
- [x] Add secrets: `Clerk:SecretKey`, `Anthropic:ApiKey`, `OpenAi:ApiKey`, `Postmark:ApiKey`, `Storage:AzureBlobConnectionString`, `IpHashing:Secret`.
- [x] Enable a system-assigned managed identity on the API App Service.
- [x] Grant that managed identity `get`/`list` access on the vault's secrets.
- [x] Add each secret to the App Service's Configuration as an app setting using the `@Microsoft.KeyVault(SecretUri=...)` reference syntax.
- [x] Confirm no real secret values are ever written into `appsettings.Production.json` or checked into git — production should read exclusively from Key Vault references.

## Phase 2 — Database (Azure Database for PostgreSQL Flexible Server)

- [x] Provision a Flexible Server instance (production tier, not Burstable, given RAG/vector query load).
- [x] Add `vector` to the server's allowed extensions list, then confirm via `SHOW azure.extensions` and `CREATE EXTENSION vector`.
- [x] Recreate the two-role split from local dev: an owner/migration role (`devlog`) and a non-superuser runtime role (`devlog_app`) with `FORCE ROW LEVEL SECURITY` actually enforced.
- [x] Run EF Core migrations against production using the owner connection string only — never the runtime connection. Must happen *before* the RLS scripts below — every script in `sql/001`-`009` says "run this after [a specific migration]" in its header, since they were written interleaved with migrations over the project's local-dev timeline. On a fresh database, run all migrations first (one command applies the full history), then the scripts.
- [x] Apply the RLS policy scripts `sql/001` through `sql/009`, in numeric order, against the new database.
- [x] Point the app's `ConnectionStrings:DevLogDbRuntime` at the `devlog_app` role, same as local dev.
- [x] Store both full connection strings (`ConnectionStrings:DevLogDb` — the admin/owner login, migrations only — and `ConnectionStrings:DevLogDbRuntime` — `devlog_app`) as Key Vault secrets, same naming convention as the six from Phase 1. Only reference `ConnectionStrings__DevLogDbRuntime` from the App Service's app settings — the running app never connects as the owner role, so `ConnectionStrings:DevLogDb` should stay in Key Vault only, never wired into App Service, keeping the admin credential out of reach of the running process entirely (used only from `dotnet ef` locally or CI/CD). Neither should ever be typed as a plain-text app setting. Use a freshly generated password for both roles — never reuse the local dev `devlog_local`/`devlog_app_local` passwords.

## Phase 3 — Compute

- [x] Create an Azure App Service (Linux, .NET 10) for the API.
- [x] Create an Azure Static Web Apps instance (or a second App Service) for the Next.js frontend.
- [x] Map the real production domain and let Azure issue a managed TLS certificate. Domain: **chuzksy.dev** (Porkbun). Final hostnames: `app.chuzksy.dev` (frontend, Static Web App) and `api.chuzksy.dev` (API, App Service) — both **Validated/Secured**, TLS confirmed live via HTTPS fetch.

  **A. Frontend — `app.chuzksy.dev`** ✅
  - Straight CNAME validation (Host `app` → `ashy-tree-0b69d760f.7.azurestaticapps.net`) repeatedly failed in the portal with a generic "An unknown error has occurred while adding your custom domain" — a known Azure Static Web Apps portal bug (confirmed: not a DNS issue, no CAA record blocking, and the CLI (`az staticwebapp hostname set`) hit the same wall).
  - Fix that worked: switched **Hostname record type** to **TXT** instead. Added TXT (Host `app`, value from Azure) at Porkbun *after deleting the existing CNAME at that host* (a CNAME can't coexist with any other record type at the same host — this conflict is also likely why the direct CNAME attempts kept failing). Once TXT validated, deleted it and re-added the CNAME (Host `app` → the azurestaticapps.net hostname). That validated cleanly.
  - Lesson for next time: if CNAME-only validation throws the generic error, don't keep retrying it — switch to TXT validation immediately instead.

  **B. API — `api.chuzksy.dev`** ✅
  - Plan tier was already Basic+; used "All other domain services" (not "App Service Domain") in the Add custom domain wizard since chuzksy.dev is external (Porkbun).
  - Added both records at Porkbun: `TXT` (Host `asuid.api`, verification ID) and `CNAME` (Host `api` → the app's `*.azurewebsites.net` default hostname). Validated on the first attempt (no portal bug hit here).
  - Bound the free **App Service Managed Certificate** via Custom domains → Add binding → SNI SSL. Status: Secured.

  **C. Verified**
  - `https://app.chuzksy.dev` — loads, valid TLS, no cert warnings.
  - `https://api.chuzksy.dev` — loads, valid TLS; currently shows Azure's default "no content deployed yet" page, which is expected — API code deployment happens in a later phase.
- [x] Add a staging deployment slot on the API App Service (deploy → smoke test → swap, never deploy straight to production). Plan was already Premium v3 P0V3 (well above the Standard S1 minimum slots require — no upgrade/cost change needed). Created `devlog-api-staging`, cloned from production.
  - Gotcha hit and fixed: staging's Key Vault secret references (`Anthropic_ApiKey`, `Clerk__SecretKey`, `ConnectionStrings__DevLogDbRuntime`, `IpHashing__Secret`) showed red/unresolved after creation. Root cause: each slot gets its own separate system-assigned managed identity — production's Key Vault role grant from Phase 1 doesn't extend to a slot created afterward. Fixed via Key Vault → Access control (IAM) → Add role assignment → **Key Vault Secrets User** → assigned to the `devlog-api/staging` managed identity (vault uses Azure RBAC permission model, confirmed via Access configuration, not the legacy Vault access policy model). Role assignment alone didn't clear the red X immediately — a **restart of the staging slot** was needed to force it to re-resolve the references. Worth remembering: any time Key Vault access is granted/changed for a slot, restart that slot rather than just waiting.
- [x] Update `Cors:FrontendOrigins`, `Clerk:AuthorizedParty`, `Frontend:BaseUrl` (used to build the recruiter email verification link — missed on the first pass of this checklist), and the frontend's `NEXT_PUBLIC_API_BASE_URL` to the real production origins.
  - [x] Frontend: created `web/.env.production.local` (gitignored) with `NEXT_PUBLIC_API_BASE_URL=https://api.chuzksy.dev` — named `.production.local`, not plain `.production`, since Next.js's env precedence checks `.env.local` before plain `.env.production`, and `.env.local` already pins this to the localhost dev API. Takes effect on the next `npm run build`.
  - [x] Backend — added as **slot settings** (checked, so they don't move on a future swap) on both the production slot and `devlog-api-staging`:
    - `Clerk__AuthorizedParty` = `https://app.chuzksy.dev`
    - `Frontend__BaseUrl` = `https://app.chuzksy.dev`
    - `Cors__FrontendOrigins__0` = `https://app.chuzksy.dev`

## Phase 4 — CI/CD (GitHub Actions)

- [x] Add a workflow that builds and runs `dotnet test` + `npm run build`/`tsc --noEmit` on every PR — this is what would have caught the Phase 0 validation-attribute regression automatically. `.github/workflows/ci.yml`, runs on `pull_request` into `main`: `dotnet restore`/`build`/`test` against `api/DevLog.slnx`, plus `tsc --noEmit`/`eslint`/`next build` for `web/` (the build step uses dummy env values deliberately — PRs from forks don't get repo secrets, and this is a structural check, not a deployable artifact).
- [x] Add `dotnet list package --vulnerable` and `npm audit` as pipeline steps (closes finding #131). Both in `ci.yml`: the NuGet check greps the report output for "has the following vulnerable packages" and fails the step itself, since the command's own exit code stays 0 either way; `npm audit --audit-level=high` (not the noisier default `low`, which would flag low-severity dev-dependency issues unrelated to the shipped app).
- [x] Enable GitHub secret scanning / push protection on the repo. **Changed approach**: GitHub's native Secret Protection (Settings → Advanced Security) isn't available — it requires an org-owned repo on a paid plan/GHAS add-on, and `devlog` is a personal-account private repo, deliberately not moved into a paid org just for this. Substituted `gitleaks` in CI instead, which needs no license key on a personal-account repo (only org-owned repos require one):
  - `ci.yml` gained a `secret-scan` job — runs `gitleaks-action` on every PR, same practical effect as push protection (blocks a PR that introduces something secret-shaped), just enforced in CI rather than natively by GitHub.
  - New `.github/workflows/secret-scan-history.yml`, manually triggered (`workflow_dispatch`) — scans the *entire* git history, not just new commits. Worth running once now as a baseline check that nothing's ever actually leaked into this repo's history.
- [x] Deploy API directly to production on merge to `main`. `.github/workflows/deploy.yml` now publishes and deploys to `app-devlog-api-prod-ca3c` in production, then runs a production `/health` smoke test before frontend deployment.
- [x] Removed staging slot swap workflow for the cost-capped footprint. The production health smoke test in `deploy.yml` is now the automated release gate before frontend deploy.
- Azure/GitHub setup for deploy workflow ✅ done — full walkthrough in the new `DevLog-Azure-OIDC-Setup-Guide.md`:
  - Azure OIDC: App Registration `devlog-github-actions`, federated credential for GitHub `production` environment, **Website Contributor** role assignment scoped to `app-devlog-api-prod-ca3c`.
  - GitHub secrets: `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID`, `CLERK_SECRET_KEY` (real `sk_live_...` production value — see below), `NEXT_PUBLIC_CLERK_PUBLISHABLE_KEY` (`pk_live_...`, intentionally sourced as a secret for this pipeline due to variable resolution issues in frontend deploy runs), `AZURE_STATIC_WEB_APPS_API_TOKEN`.
  - GitHub variables: `AZURE_RESOURCE_GROUP` (confirmed `coursera_ai_learning`).
  - Along the way: created a dedicated Clerk **Production** instance (previously only had the Development instance's `pk_test_`/`sk_test_` keys) — "Primary application" for chuzksy.dev, so Clerk's API lives at `clerk.chuzksy.dev` and auth emails send from `@chuzksy.dev`. Five CNAME records added at Porkbun for it (Frontend API, Account portal, Primary email, 2 DKIM selectors).
  - GitHub environment `production` created (Settings → Environments) so the federated credential subject claim matches.
- [x] Hardening pass on both workflow files, from an external review Chika pasted in:
  - All 8 actions across both workflows pinned to commit SHAs, not version tags (`actions/checkout`, `actions/setup-dotnet`, `actions/setup-node`, `actions/upload-artifact`, `actions/download-artifact`, `azure/login`, `azure/webapps-deploy`, `Azure/static-web-apps-deploy`) — each with a `# vX.Y.Z` comment recording which released version it corresponds to. The last 4 were blocked by GitHub's unauthenticated API rate limit initially; resolved once it cleared, using each repo's public releases page as the SHA source rather than a fabricated value.
  - `timeout-minutes` added to every job.
  - `ci.yml` cancels in-progress runs on the same branch when a new commit lands (`cancel-in-progress: true`); `deploy.yml` deliberately does not (a live deploy shouldn't get killed mid-swap by a second push).
  - NuGet restore caching added to both workflows' `setup-dotnet` steps.
  - New `GET /health` endpoint on the API (`Program.cs`, deliberately dependency-free — no DB call) plus a new `smoke-test-staging` job in `deploy.yml` that polls it with retries after the staging deploy and before the swap job's dependencies are satisfied. This is a small automated tripwire in front of the swap, not a replacement for Phase 6's full manual staging smoke test.
  - Deploy workflow uses a single `production` GitHub environment for OIDC and audit trail.
  - Declined for now: a separate scheduled dependency-audit workflow for ongoing visibility beyond PR-time — worth adding later if drift between "passed audit at merge time" and "new CVE disclosed against an already-merged dependency" becomes a real concern, but it's new scope rather than a fix to what's already here.

## Phase 5 — Observability

- [x] Create an Application Insights resource and connect it to the API App Service — this auto-injects `APPLICATIONINSIGHTS_CONNECTION_STRING` and `ApplicationInsightsAgent_EXTENSION_VERSION` as app settings; no manual Key Vault wiring needed for this one, and the OpenTelemetry Azure Monitor pipeline reads the same connection string directly. (Optional hardening: move the connection string into Key Vault and swap the value to a `@Microsoft.KeyVault(...)` reference, keeping the same setting name — lower priority than the other six secrets since this one is write-only telemetry access, not a data credential.)
- [x] Wire telemetry export in `Program.cs` via `AddOpenTelemetry().UseAzureMonitor(...)` (single pipeline to avoid duplicate Application Insights ingestion).
- [ ] Set up basic alerts: 5xx rate, p95 response time, unhandled exceptions.
  - [ ] Create an Azure Monitor **Action Group** (e.g., `devlog-prod-critical`) with at least one email receiver; optional Teams/Slack/webhook receiver.
  - [ ] Create alert rule: **API 5xx rate spike** (scope: production Application Insights resource, severity: Sev2).
    - Preferred signal: Log alert (scheduled query) over 5-minute windows, with both an error-rate threshold and a minimum traffic gate to avoid low-volume noise.
    - Suggested KQL:
      ```kusto
      let window = 5m;
      let total = requests
      | where timestamp > ago(window)
      | where cloud_RoleName == "devlog-api"
      | summarize totalCount = count();
      let failed = requests
      | where timestamp > ago(window)
      | where cloud_RoleName == "devlog-api"
      | where toint(resultCode) between (500 .. 599)
      | summarize failedCount = count();
      total
      | join kind=inner failed on 1==1
      | extend failureRatePct = iif(totalCount == 0, 0.0, (todouble(failedCount) / todouble(totalCount)) * 100.0)
      | where totalCount >= 20 and failureRatePct > 1.0
      ```
  - [ ] Create alert rule: **API p95 response time breach** (scope: production Application Insights resource, severity: Sev3).
    - Preferred signal: Metric alert on Request duration, Aggregation: `95th percentile`, Window: `10 minutes`, Threshold: start at `> 800 ms`, tune after baseline data.
  - [ ] Create alert rule: **Unhandled exception spike** (scope: production Application Insights resource, severity: Sev2).
    - Suggested KQL:
      ```kusto
      exceptions
      | where timestamp > ago(5m)
      | where cloud_RoleName == "devlog-api"
      | summarize exceptionCount = count()
      | where exceptionCount >= 5
      ```
  - [ ] Attach all three rules to the same Action Group, enable each rule, and validate once by triggering a controlled test case from staging/prod-safe diagnostics.
  - [ ] Add an Azure Workbook dashboard (requests, 5xx %, p95 latency, exception count) for pre-swap checks and incident triage.

## Phase 6 — Smoke test on staging (before every production swap)

- [ ] Full onboarding flow: sign up, GitHub import, resume upload (confirm a real Defender for Storage scan completes, not just a stub).
- [ ] Sanitize and publish a Sprint Journal entry; confirm anonymization actually strips the configured blocklist terms.
- [ ] Ask AI end-to-end (embeddings generate on publish, RAG answer returns real content).
- [ ] Submit an interview request, verify the recruiter email link, confirm the creator notification fires.
- [ ] Download a CV via the signed URL and confirm it expires after `Storage:SignedUrlLifetimeMinutes`.
- [ ] Confirm HTTPS redirect + HSTS are active (staging is non-Development, so this should now be live — this is the first environment where it's ever been testable).
- [ ] Burst-test Ask AI / interview-request-create and confirm `429` responses show up at the configured limits.

## Phase 7 — Go live

- [x] Point production DNS at the App Service / Static Web App.
- [x] Swap the staging slot into production.
- [ ] Re-run the Phase 6 smoke test against the real production URL.
- [ ] Update `GETTING-STARTED.md`, `DevLog-Build-Journal.md`, and `DevLog-MVP-Roadmap.md` with the deployment milestone and production URLs.
