# decisions.md
## 2026-08-07
### Decision
- Use a mixed alert strategy for Phase 5 observability: query-based alert for 5xx error *rate*, metric alert for p95 request latency, and query-based alert for exception spikes.

### Why
- Error-rate alerts need denominator control (minimum total request count) to avoid false positives during low traffic; query alerts handle that cleanly.
- p95 latency maps directly to a native metric percentile alert and is lower maintenance than computing percentiles manually in KQL.
- Exception spikes are naturally represented in the `exceptions` table with straightforward rolling-window thresholds.

### Trade-off
- This introduces two alert types (metric + scheduled query), so portal management is slightly more complex, but each signal uses the most reliable mechanism.

## 2026-08-07
### Decision
- Use only the Azure Monitor OpenTelemetry pipeline for API telemetry (`AddOpenTelemetry().UseAzureMonitor(...)`), and do not run `AddApplicationInsightsTelemetry()` in parallel.

### Why
- Running both pipelines can duplicate request/dependency/exception telemetry and increase Application Insights ingestion cost.

### Trade-off
- We standardize on OpenTelemetry conventions and avoid mixed SDK behavior, but must keep OTel configuration ownership clear in `Program.cs`.

## 2026-08-07
### Decision
- Derive smoke-test target URL from `azure/webapps-deploy` output (`webapp-url`) instead of hardcoding a staging hostname.

### Why
- The actual Azure App Service slot hostname included a generated regional suffix, so hardcoded hostnames failed DNS/health checks even when deployment succeeded.

### Trade-off
- Workflow expressions become slightly more complex, but deployment verification is accurate and environment-agnostic.

## 2026-08-07
### Decision
- Align GitHub OIDC federated credential subject exactly with the workflow token subject: `repo:chuzksy-codeactive/devlog:environment:staging`.

### Why
- Subject mismatch (`@orgId/@repoId` style variant) caused `AADSTS700213` and blocked `azure/login`.

### Trade-off
- Requires strict consistency between Entra federated credential config and GitHub environment naming.