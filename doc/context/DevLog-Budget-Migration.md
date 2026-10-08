# Budget deployment preparation and migration runbook

**Status: repository preparation only.** No replacement services have been provisioned,
production settings changed, data transferred, DNS changed, or Azure resources deleted.
Existing deployment and production-slot-swap workflows are unchanged.

The recommended destination is Azure Container Apps Consumption for the API and
Neon PostgreSQL 18, with PostgreSQL replacing the managed Redis quota dependency.
Retain the existing frontend, Clerk authentication, private Azure Blob Storage,
Defender malware scanning, and Key Vault initially. The approved target location is
Azure `eastus` and Neon `aws-us-east-1`; location approval is not provisioning or
data-transfer approval.

## Baseline and cost decision

Read-only inspection on September 13, 2026 established:

| Component | Verified current configuration | September accrued cost, USD |
| --- | --- | --- |
| API plan | Linux App Service P0v3, one worker, Always On | $24.36 |
| PostgreSQL | Version 18, **Burstable B2s**, 32 GiB allocation | $23.01 |
| Redis | Balanced_B0, connected application clients | $9.86 |
| Private file scanning | Defender on-upload malware scanning enabled | $3.24 |
| Frontend | Static Web Apps **Standard**, not Free | $2.29 |
| Key Vault | Retained | Less than $0.01 |

The total was $62.77 accrued, with an approximately $151.06 monthly forecast,
not a completed monthly invoice. Earlier deployment notes describing a larger
database tier or a free frontend do not represent this verified baseline.

Across 14 days the API served 5,995 requests, with mean memory of 343 MiB and a
maximum sampled average of 373 MiB. Start the container rehearsal at **0.5 vCPU /
1 GiB, minimum zero replicas, maximum one**, then verify cold starts and peak memory.
Maximum replicas is a capacity bound, not a dollar spending cap. Do not add periodic
external keep-alive traffic that prevents scale-to-zero.

The logical application database is only **9.25 MiB**, but the current server
reported **16.01 GiB outbound network traffic in 14 days**. This is not automatically
Neon-billable public transfer, but it means storage size alone cannot justify a
promise that Neon Free will suffice. Rehearse actual compute hours, public transfer,
connection lifetime, and suspension behavior before choosing the plan.

The planning target remains approximately **$15-25/month infrastructure plus an
initial $5 AI spending objective**, not a guaranteed $20-30 bill. Neon overages or a
paid plan, retained paid Static Web Apps, observability, cross-cloud transfer,
Clerk/Postmark, CI usage, taxes, and exchange rates must be included in the estimate.
Savings begin only after individually approved retirement of the replaced resources.

## What this change supplies

- A multi-stage .NET 10 API Dockerfile with a non-root runtime and HTTP port 8080.
- A manually dispatched container validation workflow; no Azure login, image
  publishing, infrastructure changes, or application deployment.
- Opt-in, durable PostgreSQL counters for the existing global HTTP request cap and
  all registered AI provider HTTP calls, including embeddings.
- Explicit quota errors and matching UI messages, rather than incorrectly blaming
  candidate hourly limits, GitHub limits, or missing provider credentials.
- Regression coverage ensuring quota denial does not leave a sanitization stuck,
  publish an entry without its embeddings, or partially save experience changes.

The API image is not a complete deployment. The frontend is a server-rendered
Next.js/Clerk application, not a static export; moving it is outside this phase.
Do not treat the development PostgreSQL 16 Compose service as a production target.

## Durable AI allowances

### Activation order

These steps require later deployment approval. They are not performed by startup
or by the manual validation workflow.

1. Back up the target database and confirm the migration owner is separate from
   the restricted application role. Apply existing schema/RLS scripts as required.
2. After `api\sql\005_create_app_role.sql`, apply
   `api\sql\010_ai_cost_control.sql` as the migration owner using a client configured
   to stop on SQL errors. This creates a private system schema and a restricted
   `SECURITY DEFINER` acquisition function. It does not add tenant-content tables.
3. Deploy the new code with `AiQuotas__Enabled=false` or omitted. Existing
   Redis/in-memory selection is unchanged while disabled.
4. After schema and permission verification, enable the following consistently on
   every replica/revision serving the application, then restart/revise the app:

```text
AiQuotas__Enabled=true
AiQuotas__MaxProviderCallsPerMinute=20
AiQuotas__MaxProviderCallsPerDay=100
AiQuotas__MaxProviderCallsPerMonth=1000
AiCostControl__Enabled=true
AiCostControl__KillSwitchEnabled=false
```

These positive limits are starting **call-count allowances**, not a dollar or token
budget. An embedding batch counts as one provider HTTP call; a recruiter answer
usually needs an embedding call and a generation call. Failed calls, cancellations
after reservation, and provider timeouts consume the reservation conservatively.
Changing model, input size, output-token limits, batch size, or provider pricing
changes dollar cost without changing the call count. Use provider-side enforced
spending controls where available; otherwise alerts are only alerts. Calibrate
allowances against measured billed token usage before advertising a $5 hard limit.

When enabled, PostgreSQL is selected **instead of Redis**, even if a legacy Redis
connection string remains configured. Redis is not connected in that mode.
Selection happens at application startup; treat changing the enabled flag as a
revision/restart operation, not a hot-switch procedure.

All replicas sharing the same database share the allowance. The limits must also
match across replicas: a more permissive replica could otherwise admit additional
calls. Do not send traffic to older, quota-disabled revisions during activation.
Staging should use its own isolated database and provider project/key; otherwise
its calls consume production allowances.

### Accounting and errors

The private schema contains at most two rows: `requests` for the existing global
HTTP minute cap, and `provider_calls` for minute/day/month AI allowances. Counters
are reserved atomically under a row lock using the database clock. Windows are
fixed calendar windows in UTC, not sliding windows. Calendar-boundary bursts are
possible; existing per-user/IP and tenant limits remain additional protections.

Reservations commit on a separate connection with ambient transactions suppressed,
so an application rollback cannot refund provider usage. An uncertain database
outcome never causes an unmetered provider request. Denied acquisitions do not
consume other counters. Rollovers reuse the same rows; no cleanup job or continuously
running worker is necessary. Reapplying the SQL script does not reset counters.

The runtime role may execute the function but cannot directly read, edit, or delete
counter rows. Limits are supplied by trusted application configuration, not request
parameters. These application-wide counters are cost controls, **not tenant
authorization**; existing RLS and request authorization remain required.

| Condition | HTTP status | JSON `reason` |
| --- | --- | --- |
| Provider minute allowance exhausted | 429 | `ai_provider_minute_allowance_reached` |
| UTC daily allowance exhausted | 429 | `ai_daily_allowance_reached` |
| UTC monthly allowance exhausted | 429 | `ai_monthly_allowance_reached` |
| Quota database/function unavailable | 503 | `quota_store_unavailable` |
| Existing AI kill switch enabled | 503 | `kill_switch_enabled` |

Errors retain the existing `{ "error": "...", "reason": "..." }` response convention.
The gateway covers OpenAI generation, Anthropic generation, and OpenAI embeddings;
new provider HTTP clients must attach `AiQuotaHttpHandler`. There is no in-memory
fallback on a quota-store outage.

Sanitization persists its successful preview directly as PendingApproval, rather
than committing an intermediate Sanitizing state before an external call.
Publication and experience changes are committed with their new embeddings after
successful generation. Existing private resume upload/scanning may complete before
suggestion generation is rejected; the independent upload is intentionally not
deleted merely because the AI allowance is exhausted.

An operator using the owner connection can inspect counters without seeing user
content:

```sql
SELECT scope, minute_start, minute_count, day_start, day_count, month_start, month_count
FROM devlog_cost_control.ai_quota_counters;
```

Counter values belong to their stored UTC windows and are refreshed lazily on
successful acquisition. They are reservations, not a billing ledger. Restoring an
older database backup can roll back reservations; keep AI paused after restore and
reconcile the applicable allowance with provider usage before re-enabling it.

## Local and manual validation

API tests use the existing .NET 10 test project. Ordinary runs visibly skip the real
PostgreSQL quota tests unless `DEVLOG_QUOTA_TEST_CONNECTION` is set. Those tests
require a **dedicated disposable PostgreSQL 18 server on localhost**, with a role
that can create databases and roles. They create uniquely named test databases and
roles and remove only those resources. Never point them at a shared development,
staging, or production server.

Example using a disposable local Docker server from PowerShell:

```powershell
docker run --detach --name devlog-quota-tests --publish 127.0.0.1:55432:5432 `
  --env POSTGRES_PASSWORD=local-disposable-test-only postgres:18
# Wait for: docker exec devlog-quota-tests pg_isready -U postgres
$env:DEVLOG_QUOTA_TEST_CONNECTION = "Host=127.0.0.1;Port=55432;Database=postgres;Username=postgres;Password=local-disposable-test-only"
dotnet test api\DevLog.Tests\DevLog.Tests.csproj --configuration Release `
  --filter "FullyQualifiedName~AiQuota|FullyQualifiedName~AiCostControlMiddlewareTests"
# After the test run, remove only this disposable test container:
docker rm --force --volumes devlog-quota-tests
Remove-Item Env:\DEVLOG_QUOTA_TEST_CONNECTION
```

Use Node.js 22.6+ (Node 24 recommended) for the dependency-free quota-message tests:

```powershell
node --experimental-strip-types --test web\tests\ai-quota-errors.test.mjs
docker build --file api\Dockerfile --tag devlog-api:local api
```

The **Container validation** GitHub Actions workflow is manual-only. It runs
the PostgreSQL quota tests against a disposable PostgreSQL 18 service, builds the
image, verifies its configured non-root user, and smoke-tests `/health`. It does not
publish an image or require production credentials. GitHub may require this workflow
on the default branch before exposing its Run workflow button.

`/health` is a process liveness endpoint, not a database, RLS, authentication, storage,
or AI-readiness assertion. A successful container smoke test is not cutover approval.

## Provisioning and rehearsal gate

Obtain explicit approval for the revised estimate, region/data-location implications,
recovery policy, and proposed resources before provisioning anything. Registering
`Microsoft.App`, publishing an image, creating a Neon project, and configuring a
new Container App are changes, even before DNS is switched.

The deployment design must address:

- Container Apps Consumption at zero-to-one replicas initially, HTTP target port
  8080, managed TLS, bounded ingress/concurrency, and sufficient startup-probe
  allowance for cold starts. Do not expose a production HTTP-only public endpoint.
- A tested trusted-ingress configuration for forwarded scheme and client IP.
  The current API supports explicit known proxy IPs. Establish the actual Container
  Apps proxy boundary and, if required, add narrowly allowlisted network support
  before cutover. Do not solve this by trusting arbitrary forwarded headers.
- Private container image access, immutable digests, patch rebuilds, and rollback
  retention. Consider GHCR before adding a paid ACR; verify current package and CI
  allowances rather than assuming every registry operation is free.
- Secrets supplied at runtime using approved secret storage/managed identity.
  Never bake API keys, database dumps, `.env` files, or development settings into
  an image. Preserve the existing strong IP-hashing secret and Clerk issuer,
  authorized party, frontend origins, and authenticated dashboard behavior.
- Ephemeral container filesystems. The API currently uses Clerk bearer JWTs and has
  no application-level ASP.NET Data Protection consumers. If protected cookies or
  tokens are introduced, configure a persistent encrypted key ring rather than
  relying on container-local keys that disappear during scale-to-zero or replacement.
- Azure Blob private access and Defender on-upload scanning, including quarantine
  and fail-closed behavior. Do not substitute the public local-disk development
  provider. Separately approve any change to RAGRS redundancy or the 10,000 GB/month
  scanning cap.
- Observability retention, sampling, ingestion limits, and subscription/provider
  budget notifications. Logging entire prompts, CVs, tokens, or raw employer content
  is prohibited. Budget notifications do not stop spending.

### PostgreSQL and Neon requirements

Use **PostgreSQL 18 plus pgvector**, not an implicit downgrade to the local Compose
version. Confirm installed extension versions and HNSW/vector index compatibility.
Use a direct Neon hostname, **not the transaction-pooling `-pooler` endpoint**:
`TenantConnectionInterceptor` sets session-scoped `app.current_tenant` on connection
open, which transaction pooling does not preserve correctly.

Use bounded Npgsql application pooling (initial maximum 10 connections per replica),
normal connection reset, certificate validation (`SSL Mode=VerifyFull`), finite
connection/command timeouts, and no continuous database health polling that prevents
suspension. Never configure `No Reset On Close=true`. Verify tenant context after
pool reuse, restart, cold start, and database resume.

Create `devlog_app` with SQL as a separate restricted login. It must be **NOSUPERUSER,
NOBYPASSRLS**, must not own tenant tables, and must not inherit an owner or
`neon_superuser` role. Neon console-created roles may have elevated inheritance.
Apply and verify the existing RLS policies and explicit grants as well as script
010. Do not reuse the owner connection as the API runtime connection.

Before moving production data, establish an independent encrypted backup and
restore process with least-privileged access, retention, expiry, and deletion
procedures. Current Azure recovery is seven-day PITR; Neon Free's short restore
history is **not equivalent**. A proposed daily backup with seven-day retention
still means a potential 24-hour data loss, not seven-day PITR. Agree an explicit
RPO/RTO and rehearse a timed restore before accepting that downgrade.

Measure at least a representative week of Neon compute and public transfer, including
resume imports, recruiter questions, embeddings, cold starts, and monitoring.
Free storage fit does not prove compute/transfer fit. If allowances or recovery
requirements do not fit, present the cheapest suitable paid plan or an adjusted
Azure configuration for approval rather than allowing an unexpected suspension.

## Controlled cutover and rollback

1. Rehearse restore and application behavior on an isolated target. Verify all
   tables, row counts, constraints, sequence values, extensions, vector indexes,
   restricted-role permissions, and two-tenant isolation. Never export production
   records into Git, build artifacts, or CI logs.
2. Verify public/private portfolio boundaries, Clerk sign-in, sanitization and
   approval, recruiter RAG, experience updates, private CV scanning/access, email
   links, CORS, forwarded HTTPS/client IP, and quota failure behavior on the target.
   Use bounded approved AI test spend. Non-AI portfolio browsing must continue
   when AI is intentionally paused.
3. Obtain separate cutover approval. Record image digest, source/target schema
   versions, configuration references, a final backup, and rollback criteria.
   Stop **all** writes and AI calls on the old revision/slot before final export.
   The AI kill switch alone is not a write-maintenance mode.
4. Perform the rehearsed final export/restore, reconcile counts and quota state,
   run read-only tenant-isolation checks, route traffic, then reopen writes on
   exactly one database. Keep the former deployment inaccessible to normal traffic.
   Do not run both databases as independent writable primaries.
5. Before target writes begin, rollback may route to the unchanged old stack.
   **After any target writes, never simply revert DNS or a connection string.**
   Freeze writes, take a target backup, reconcile/migrate the new data and quota
   usage back using the rehearsed procedure, verify integrity, then reopen only
   the selected primary. A forward fix may be safer than reverse migration.
6. Monitor real cost and error/latency/cold-start metrics through the agreed
   observation window. Keeping both stacks provisioned during rollback retention
   temporarily increases cost; set an explicit retirement deadline.

For a code-only quota rollback, pause AI first. Restore the legacy Redis configuration
and availability **before** disabling `AiQuotas` and restarting, if returning to the
old global-cap implementation. Falling back to per-process counters removes durable
cross-replica protection and requires an explicit decision. Do not delete or zero the
new counters as a routine rollback, and do not drop script 010 while enabled
revisions still serve traffic.

Retire only individually identified resources after explicit approval and confirmation
that no other workload depends on them: API/staging resources and their App Service
plan, the old database after the agreed backup window, then managed Redis after
observing PostgreSQL enforcement. **Stopping an App Service app does not stop its
plan charges.** Do not delete an entire resource group: it contains unrelated
learning/AI resources. Retain frontend, storage/scanning, and authentication unless
a separate decision changes that scope.

## Reference checks

Provider limits and prices change. Recheck before provisioning:

- [Azure Container Apps billing](https://learn.microsoft.com/azure/container-apps/billing)
- [Neon pricing and plan limits](https://neon.com/pricing)
- [Neon connection pooling](https://neon.com/docs/connect/connection-pooling)
- [Neon roles](https://neon.com/docs/manage/roles)
- [Neon regions](https://neon.com/docs/introduction/regions)
