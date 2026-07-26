# DevLog — MVP & Phasing Roadmap

Everything in the product brief and architecture doc, sequenced into three phases. The goal of Phase 1 isn't "build the full vision smaller" — it's the minimum that can test the core hypothesis: a rich, AI-queryable sprint journal gets more recruiter engagement and interview requests than a normal CV. Everything not load-bearing for that test is deferred.

---

## Status as of 2026-07-26 (Milestone 11 complete)

Phase 1 is functionally complete for personal use. Two things worth naming explicitly:

- **Deviation:** Phase 1 called for "instrument everything, ship no dashboard." Milestone 11 built a full creator CRUD dashboard instead, once the goal shifted from external dogfooding to daily personal use for Chika and his partner. This dashboard is *not* the same thing as Phase 2's "creator analytics dashboard" (view-count/engagement data) — that item is still not started.
- **Deferred gaps (Phase 1 items not built, deliberately left for later):** `portfolio_view_events` logging was never implemented, and `Subscription.SanitizationQuota` exists as a schema field but has no enforcement code anywhere. Both are cost/abuse controls that matter most at multi-tenant scale with strangers using the product — low-risk to defer while usage is just the two of you. Ask AI's separate hourly rate cap (`ai_query_logs`-based) *is* implemented and working.

Status tags below: ✅ done · ⚠️ deviated (built differently than specified) · ⏸️ gap, deferred · ⬜ not started.

---

## Recommendation before Phase 1: dogfood before opening signups

Launch with yourself as tenant #1, plus a handful of invited developers, before any public sign-up flow. This buys you two things cheaply: real sprint journal content to judge whether the anonymization pipeline actually produces readable, non-generic case studies, and real recruiter reactions before you've invested in onboarding polish or discovery. Multi-tenant architecture is unavoidable either way (already decided), but *opening* signups can wait a few weeks behind *supporting* them.

---

## Phase 1 — MVP: prove the hypothesis

**Creator side:** Home, Experience (manual entry — a plain form, no GitHub import or resume parsing yet), Sprint Journal with entry creation, and the full anonymization pipeline (raw → sanitize → human approval → publish). The anonymization pipeline is not a corner to cut even at MVP scale — it's the trust mechanism the whole positioning depends on, and skipping it to move faster would undermine the one thing you can't easily retrofit credibility for later. Visibility is limited to `public`/`unlisted` — `paused` can wait, but the public/unlisted distinction is a launch blocker for anyone currently employed and writing about live work. **✅ Done** — plus a full creator CRUD dashboard (Milestone 11) beyond what this section originally scoped.

**Recruiter side:** anonymous browsing of published portfolios (no login required to read, per the AuthZ design), single-candidate Ask AI, and Request Interview. Request Interview ships in Phase 1 even though it feels like it could wait — it's the actual conversion event the whole product is built around, and without it you can't tell whether richer content is producing more than just idle interest. **✅ Done**, including recruiter email verification (Milestone 10).

**Instrument everything, ship no dashboard.** `portfolio_view_events`, `ai_query_logs`, and `interview_requests` should all be populated from day one, even though the creator-facing analytics *page* is a Phase 2 item. The logging is cheap to add alongside each feature as it's built; the UI to visualize it isn't needed until there's enough data and enough users to make a dashboard worth more than a SQL query. Query the tables directly for now — that's how you'll actually validate the hypothesis in these first weeks. **⚠️ Deviated + ⏸️ partial gap** — a creator dashboard was built after all (Milestone 11, reasoned pivot toward personal daily use rather than external dogfooding). `ai_query_logs` and `interview_requests` are populated and enforced (Ask AI's hourly rate cap runs off `ai_query_logs`). `portfolio_view_events` was never built — deferred, low risk at current (personal) usage scale.

**Technical scope:** the full multi-tenant schema and RLS isolation from Section 1 (not optional — retrofitting tenant isolation after data exists is much more expensive than building it in from the first row), the RAG pipeline from Section 2 running on the cheapest model tier throughout Phase 1 (no revenue yet to justify the paid tier's stronger model), and Sections 3–4 in full. Billing infrastructure is explicitly out of scope for Phase 1 — track usage against quotas (`subscription.sanitization_quota`, `recruiter_accounts.query_quota_monthly`) so the numbers exist, but don't build Stripe integration until Phase 2, when there's a userbase worth charging. **✅ Done**, except `subscription.sanitization_quota` tracking (see security items below).

**Suggested build order within Phase 1:** multi-tenant schema + auth first (everything else depends on it), then Sprint Journal entry creation plus the anonymization pipeline together (this is the core differentiator — get it working and get a few real entries through it before anything else), then Experience and Home (comparatively simple, mostly wiring), then the embedding pipeline and Ask AI (needs published content to embed, so it naturally comes after), then Request Interview last among the core features since it's a simpler, self-contained addition once the rest exists. **✅ Followed as written.**

**Security items that belong in Phase 1, not as a later hardening pass (see Section 9 of the architecture doc for detail):** RLS policies on `published_entries`, `embeddings`, `interview_requests`, and `ai_query_logs` — not just `raw_entries` as originally written up; prompt-injection–resistant handling of retrieved RAG context (both a recruiter-input vector and a stored-content vector via entry content); output sanitization on rendered entry content to prevent stored XSS; a per-tenant rate cap on sanitization runs, separate from the monthly quota, since that's an LLM-cost abuse vector; secrets in Azure Key Vault rather than app config; a scoped CORS policy; and recruiter email verification. None of these are exotic — they're cheap to build in now and expensive to retrofit once there's real tenant data and a live recruiter audience. **✅ Done: RLS (full coverage, verified, plus the `devlog_app` non-superuser role fix so RLS actually applies), output sanitization, scoped CORS, recruiter email verification. ⏸️ Deferred gap: the per-tenant sanitization rate cap — `Subscription.SanitizationQuota` exists in the schema but nothing enforces it yet.** Secrets/Key Vault and prompt-injection defense not re-verified in this pass.

---

## Phase 2 — Growth: remove friction, add discovery

Once Phase 1 has validated that the core loop works (recruiters engage with sprint entries, some interview requests come through), the next constraint is getting more tenants and more recruiters in the door without your direct involvement in each one.

**Creator side:** guided onboarding (GitHub import, resume upload with AI-assisted structured extraction, AI-assisted first sprint entry draft — all routed through the same sanitize-and-approve pipeline, nothing bypasses it), Learning Lab plus its retention reminder, the creator analytics dashboard (built on data already being logged since Phase 1), and the `paused` visibility state.

**Recruiter side:** Discover, built in its cheaper layer first — tag/tech facet filtering needs no LLM calls and is close to free to run — with the AI-powered cross-candidate query following once there are enough public, discoverable tenants for cross-candidate search to return something better than a handful of results.

**Multi-role expansion.** Phase 1 stays developer-only, matching the dogfood-with-developers plan. Phase 2 adds a `discipline` field to `portfolios` (software engineer, PM, BA, DevOps, Scrum Master, etc.) that drives which labels and tag taxonomy render — "Technical Design" becomes "Approach," "Core technologies" becomes "core skills/tools," and GitHub becomes one of several optional proof links (portfolio site, published articles, certifications) rather than a fixed field. This is a labeling/config exercise on top of already-generalized mechanics (the sprint case-study shape and the AI assistant don't care about discipline), not a rebuild. The product name and tagline ("DevLog," "developer career operating system") are a separate, deliberate branding decision to revisit at this point, not something to let drift by default.

**Monetization:** this is where actual billing shows up — Stripe (or equivalent) for the creator paid tier (unlimited sanitization, custom domain) and the recruiter paid tier (higher AI query volume, cross-candidate search, saved candidates). Recruiter accounts become a first-class, tiered concept rather than the lightweight per-request record they were in Phase 1.

---

## Phase 3 — Scale & monetize further

By this point the core product and its economics are validated. What's left is what strengthens trust and reach rather than what proves the concept: the peer-confirmation trust signal on entries, the recruiter "follow portfolio for updates" feature (with its opt-in visibility control), and B2B white-label licensing to bootcamps or developer communities — the highest-leverage revenue lever, but also the one that presupposes a working, credible core product to license in the first place. Revisit the pgvector-vs-dedicated-vector-DB decision here too, if per-tenant corpus size or query volume has grown enough to make it worth reconsidering (see Section 2's stated trigger condition).

---

## Feature-by-phase summary

| Feature | Phase | Status | Why here |
|---|---|---|---|
| Multi-tenant schema, RLS, AuthN/AuthZ | 1 | ✅ Done | Foundational; expensive to retrofit later |
| Home, Experience (manual entry) | 1 | ✅ Done | Minimum viable "what a recruiter expects to see" |
| Sprint Journal + anonymization pipeline | 1 | ✅ Done | The core differentiator and the trust mechanism it depends on |
| Single-candidate Ask AI | 1 | ✅ Done | The other core differentiator |
| Request Interview | 1 | ✅ Done | The actual conversion event being tested |
| Visibility: public/unlisted | 1 | ✅ Done | Privacy launch blocker |
| Event logging (views, queries, requests) | 1 | ⚠️ Partial — `ai_query_logs`/`interview_requests` done; `portfolio_view_events` ⏸️ deferred | Needed to validate the hypothesis, cheap to add now |
| Full RLS coverage, prompt-injection defense, output sanitization, secrets management, CORS, recruiter email verification | 1 | ⚠️ Mostly done — sanitization rate cap ⏸️ deferred | Cheap now, expensive to retrofit once real tenant/recruiter data exists — see Section 9 |
| Creator CRUD dashboard (not originally scoped for Phase 1) | 1 | ⚠️ Deviated — built in Milestone 11 | Personal daily-use pivot; not the same as Phase 2's analytics dashboard below |
| Onboarding (GitHub import, resume parse, AI-assisted draft) | 2 | ⬜ Not started | Removes blank-page friction once scaling past hand-held signups |
| Learning Lab + retention nudge | 2 | ⬜ Not started | Secondary differentiator, not required to test the core loop |
| Creator analytics dashboard | 2 | ⬜ Not started | Data exists since Phase 1; UI follows once there's enough to show |
| Discover (facet filter, then AI cross-candidate) | 2 | ⬜ Not started | Needs enough public tenants to be useful |
| Billing (creator + recruiter paid tiers) | 2 | ⬜ Not started | Defer until there's a userbase worth charging |
| Visibility: paused state | 2 | ⬜ Not started | Nice-to-have privacy refinement, not a launch blocker |
| Resume upload hardening, webhook signature verification, Discover visibility enforcement at SQL level | 2 | ⬜ Not started | Ships alongside the feature it protects |
| Multi-role support (`discipline` field, generalized labels, flexible proof links) | 2 | ⬜ Not started | Phase 1 stays developer-only by choice (dogfood plan); mechanics already generalize, so this is labeling, not a rebuild |
| Naming/positioning decision ("DevLog," dev-specific tagline) | 2 | ⬜ Not started | Revisit deliberately once broadening beyond developers, rather than letting it drift |
| Trust/verification (peer confirmation) | 3 | ⬜ Not started | Strengthens an already-working product |
| Follow / subscribe to updates | 3 | ⬜ Not started | Nice-to-have engagement loop, real privacy surface to get right |
| B2B white-label licensing | 3 | ⬜ Not started | Highest leverage, but presupposes a validated core product |
| Entry confirmation token hardening | 3 | ⬜ Not started | Ships alongside the trust-signal feature |
