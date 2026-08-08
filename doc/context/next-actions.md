# next-actions.md
## Next 1
- Task: Configure Azure Monitor / Application Insights alerts for production reliability.
- Done when: One Action Group exists and is attached to three enabled rules: (1) 5xx rate > 1% for 5m with request-count floor, (2) p95 request duration > 800ms for 10m, (3) exception count spike over 5m. A workbook dashboard (requests/failure %/p95/exceptions) is also saved.

## Next 2
- Task: Run full Phase 6 staging smoke test checklist end-to-end.
- Done when: Every checklist item passes on staging, including onboarding flows, sanitize/publish, Ask AI, interview request + verification + creator notification, signed CV expiry, HTTPS/HSTS behavior, and rate-limit enforcement.

## Next 3
- Task: Validate Postmark delivery path with a real Server API token and confirm creator notification delivery for interview requests.
- Done when: A real interview request flow produces a delivered creator email, with no send failures in API logs.

## Next 4
- Task: Keep context continuity disciplined after each merge.
- Done when: `current-state.md`, `decisions.md`, `deployment-status.md`, and `next-actions.md` are updated in the same PR (or immediately after merge) for every infra/behavior change.