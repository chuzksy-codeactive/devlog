# DevLog Launch Sprint Plan (v1)

## Planning assumptions
- Sprint length: 2 weeks
- Timeline: 5 sprints
- Primary launch segment (Ideal Customer Profile, ICP): backend/cloud developers with 2–8 years of experience (YOE) actively job-seeking
- Goal: production-ready v1 with secure creator workflows and credible recruiter discovery

## Sprint 1 — Foundation, Security, and Measurement
**Objective:** Secure multi-tenant base and measurable funnel.

| Ticket | Title | Description | Acceptance Criteria |
|---|---|---|---|
| DEVLOG-001 | Define ICP in-product | Lock ICP in copy, onboarding text, and analytics properties. | ICP segment is selectable and trackable in analytics events. |
| DEVLOG-002 | AuthN/AuthZ role matrix | Implement Creator vs Viewer route-level permission map. | Unauthorized access returns 403 on protected endpoints. |
| DEVLOG-003 | Tenant isolation guardrails | Enforce tenant_id checks in all portfolio/log queries. | Cross-tenant data access is blocked in API and tests. |
| DEVLOG-004 | Event instrumentation baseline | Add events: signup, first-log-start, first-log-save, first-publish, profile-share, recruiter-query. | Events appear in analytics dashboard with user/session IDs. |
| DEVLOG-005 | Error tracking and audit logging | Add centralized error logging and audit trail for publish/redaction actions. | Action logs are visible per entry with actor and timestamp. |

## Sprint 2 — Creator Activation and Log Authoring
**Objective:** Reduce blank-page friction and time-to-first-value.

| Ticket | Title | Description | Acceptance Criteria |
|---|---|---|---|
| DEVLOG-006 | Guided onboarding wizard | Build a 4-6 step wizard to create the first sprint case study. | New user reaches a draft case study in one guided flow. |
| DEVLOG-007 | Structured sprint template | Add fields: Problem, Constraints, Actions, Stack, Impact, Lessons. | Required fields are validated before publish. |
| DEVLOG-008 | Draft autosave and resume | Autosave every X seconds and restore interrupted drafts. | Reload restores latest draft state reliably. |
| DEVLOG-009 | Import helper (notes/commit summary) | Paste raw notes and generate the first structured draft. | Generated draft fills template sections for user edit. |
| DEVLOG-010 | Publish readiness checklist | Quality checks for metrics, tags, and redaction completion. | Publish is disabled until mandatory checks pass. |

## Sprint 3 — Privacy and Anonymization Trust
**Objective:** Make sanitization reliable and visible.

| Ticket | Title | Description | Acceptance Criteria |
|---|---|---|---|
| DEVLOG-011 | Sanitization pipeline hook | Process entry text before persistence to public-readable store. | Raw text never appears in the public query path. |
| DEVLOG-012 | Redaction preview UI | Show original vs sanitized diff (creator-only) pre-publish. | User can review and approve every flagged redaction. |
| DEVLOG-013 | Redaction confidence and manual override | Add confidence scores and editable replacements. | Low-confidence items require explicit user confirmation. |
| DEVLOG-014 | Unsafe publish hard block | Block publish if high-risk PII/proprietary markers remain. | Publish API rejects unsafe content with specific reasons. |
| DEVLOG-015 | Visibility controls | Per-entry visibility: private, unlisted, public. | Viewer endpoints return only allowed visibility states. |

## Sprint 4 — Recruiter Experience and AI Assistant (RAG)
**Objective:** Trustworthy, fast recruiter answers from candidate-specific data.

| Ticket | Title | Description | Acceptance Criteria |
|---|---|---|---|
| DEVLOG-016 | Metadata normalization | Enforce tags: cloud, db, language, scope, scale, role, outcomes, timeframe. | All published entries are indexed with normalized schema. |
| DEVLOG-017 | Vector indexing pipeline | Chunk and embed sanitized entries with tenant/user scope. | New publish updates vector index within defined SLA. |
| DEVLOG-018 | Candidate-scoped retrieval | Retrieve only selected candidate corpus for recruiter queries. | No cross-candidate leakage in retrieval tests. |
| DEVLOG-019 | Citation-first AI responses | Every answer includes source snippets/entry links. | Responses show citations with jump-to-entry behavior. |
| DEVLOG-020 | Recruiter filters and saved queries | Build fast filters and saved search presets. | Filter interactions return under target latency. |

## Sprint 5 — Launch Hardening, Performance, Packaging
**Objective:** Production readiness and go-to-market control.

| Ticket | Title | Description | Acceptance Criteria |
|---|---|---|---|
| DEVLOG-021 | Performance optimization pass | Optimize timeline, filtering, and AI query latency. | Meets launch SLOs with explicit p95 targets. |
| DEVLOG-022 | AI quality feedback loop | Add "report bad answer" workflow and triage path. | Flagged answers are stored and reviewable in admin queue. |
| DEVLOG-023 | Redaction QA workflow | Add "report bad redaction" path and internal review queue. | Security triage dashboard is operational. |
| DEVLOG-024 | Pricing and plan gating | Define free vs pro limits (entries, queries, exports, branding). | Feature gates are enforced server-side and in UI. |
| DEVLOG-025 | Launch readiness dashboard | One dashboard for activation, publish rate, recruiter utility, retention. | Daily KPI view is available for launch decisions. |

## Suggested epic structure
1. **EPIC-A:** Security and Multi-Tenant Core (DEVLOG-002, DEVLOG-003, DEVLOG-005)
2. **EPIC-B:** Creator Activation (DEVLOG-006, DEVLOG-007, DEVLOG-008, DEVLOG-009, DEVLOG-010)
3. **EPIC-C:** Anonymization and Trust (DEVLOG-011, DEVLOG-012, DEVLOG-013, DEVLOG-014, DEVLOG-015)
4. **EPIC-D:** Recruiter AI and Discovery (DEVLOG-016, DEVLOG-017, DEVLOG-018, DEVLOG-019, DEVLOG-020)
5. **EPIC-E:** Launch Operations (DEVLOG-021, DEVLOG-022, DEVLOG-023, DEVLOG-024, DEVLOG-025)

## Execution sequence rationale
1. Secure the platform and tenant boundaries first.
2. Prove creator value with low-friction publishing.
3. Build visible trust controls around anonymization.
4. Deliver recruiter-side utility with scoped RAG and citations.
5. Harden performance, quality loops, and monetization gates for launch.
