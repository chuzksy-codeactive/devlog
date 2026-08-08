# deployment-status.md
## Phase
- Current: **Phase 5 (Observability)** in progress.
- Phase 4 (CI/CD deploy pipeline): functionally unblocked and validated with successful `push` deploy run.
- Phase 5 checklist now includes a concrete implementation runbook for alerts and dashboard setup; execution in Azure is still pending.
- Phase 7 cutover actions (DNS + staging→production swap) are complete; remaining go-live work is production smoke re-run and final doc updates.

## Latest workflow runs
- Swap staging → production: **success** — run `31261878245`, commit `790fa2ef105766347e388c3419b50725ce848ab1`.
- Deploy (main): **success** — run `31257949263`, commit `7ce68aeb17451b8810083911462f3857da50d7c0` (PR #9 merge commit).
- Deploy (main): **success** — run `31215484705`, commit `f4b8bb09ff9be86ebb208d8162d36d59cd929f2c` (PR #7 merge commit).
- Deploy (main, prior): **failure** — run `31212332118`, commit `3d8d4773b8d59b0892300a5662d1e0350c5bb30a` (failed at old smoke-test URL before fix).
- CI (main): no recent `main` CI runs expected; CI is primarily PR-triggered.

## Known issues
- GitHub Actions warning remains about some actions targeting Node.js 20 while runners are on Node.js 24 (non-blocking warning seen in deploy jobs).
- Phase 5 alert rules are not configured yet (`5xx`, `p95`, `exceptions`).
- Phase 6 full staging smoke checks still pending (onboarding, sanitize/publish, Ask AI, interview request email flow, signed CV expiry, rate-limit checks).