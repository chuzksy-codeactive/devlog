# DevLog — Product Brief (Refined)

## Positioning statement

DevLog is not a portfolio. It's a **developer career operating system**: a living record of how a developer thinks, decides, and grows — built to move a recruiter from "interesting profile" to "let's talk" faster than a CV or LinkedIn ever could.

The wedge: everyone else shows *what* you know. DevLog shows *how you work* — through real sprint case studies, real trade-off decisions, and an AI that can answer "does this person have real Azure experience?" with evidence, not keywords.

---

## Refined information architecture

Your original structure is right, but 8 top-level sections risks becoming LinkedIn-shaped: a list of pages a recruiter has to click through one by one. Recruiters give a portfolio ~30 seconds before deciding whether to keep reading. Collapse to five surfaces, and turn everything else into **filterable content types inside them**, not separate nav items.

**1. Home** — hero with photo, one-line positioning (not a generic "About me"), current role, years of experience, core tech stack, GitHub, CV download — and the AI ask-box embedded directly in the hero, not buried in a separate tab. This is the hook; don't make them scroll to find it. "Current role" and "years of experience" need a graceful non-employed state — "Actively building" or "Aspiring [role]" instead of a title, and no hard-coded "0 years" stat forced into a prominent spot for someone without formal experience yet. Treat this as a real state to design for, not an edge case to patch later.

**2. Experience** — promoted to its own page (revised from the earlier draft, which buried this inside Timeline & Journal). Reasoning: a recruiter's first pass and a recruiter's deep-dive pass are genuinely different jobs-to-be-done — "does this person have the years/stack/companies I need" versus "how do they actually work" — and conflating both on one page made the first pass compete with the second for layout and attention. Experience gets a clean, streamlined, CV-like vertical timeline: company (sanitized descriptor), title, duration, tools, key achievement, one role after another, built for a fast scan and easy to make print/export-friendly alongside the CV download. Each role links forward into the filtered Sprint Journal entries from that period, so the connective tissue between "what I did" and "how I did it" survives the split — it's two destinations, not two disconnected ones.

**Beyond developers.** The core mechanics (sprint case studies, the AI assistant, anonymization) don't actually depend on being a developer — a PM, BA, DevOps engineer, or Scrum Master all work in sprints and can all write the same case-study shape. Decided to keep Phase 1 developer-only (matches the dogfood plan already in the roadmap) and generalize in Phase 2: a `discipline` field on `portfolios` driving label/taxonomy changes ("Technical Design" → "Approach," "Core technologies" → "core skills/tools," GitHub becoming one of several optional proof links). The product name and tagline are dev-specific branding and are a separate decision to make deliberately when broadening, not now.

**Experience isn't employer-only.** DevLog has to work for someone without a job yet — a new grad, bootcamp graduate, or career switcher — not just for someone with a formal work history. Each entry carries a type (employer, personal project, open source, freelance, education) rather than assuming every row is a paid role. This isn't a fallback state — for exactly this audience, the Sprint Journal case-study format is arguably the stronger pitch than it is for someone with formal experience, since it proves capability directly instead of relying on a job title a recruiter has to take on faith.

**3. Sprint Journal** — now a page of its own with a clear single job: sprint case studies, feature showcases, and engineering challenges, filterable by technology, problem domain, and seniority of the task. This is the differentiator, and it reads more like a technical blog / case-study archive now that it isn't sharing a page with a CV-style list. Each sprint case study keeps your original structure: Title, Delivered, Problem, Solution, Technical Design, Challenges, Result.

**4. Learning Lab** — monthly diary + career retrospectives, unified as one growth log rather than two separate destinations.

**5. Ask AI** — persistent floating assistant available on every page (not a standalone page), so a recruiter can ask a question at any point without losing their place.

Why this still isn't LinkedIn-shaped: the risk was never "more than one page" — it was feed logic, connections, endorsements, vanity metrics. A dedicated Experience page is a normal, expected convention (most strong portfolio sites have one) as long as its visual language stays narrative rather than tabular/corporate. Five focused surfaces with a clear job each is still leaner and more scannable than the original eight, and it now matches how recruiters actually triage: scan Experience first, then only spend real time in Sprint Journal for candidates who already look like a fit.

**Two distinct apps, not one.** Worth naming explicitly now that discovery and recruiter workflow are in scope (see Gap closure, below): DevLog is actually two surfaces sharing one backend — (a) each creator's public portfolio (Home, Experience, Sprint Journal, Learning Lab, plus the persistent Ask AI and Request Interview actions), and (b) a recruiter-facing app (Discover/search across candidates, saved candidates, an interview-requests inbox) that recruiters land on when they arrive at DevLog directly rather than via a shared portfolio link. This distinction didn't need to exist while the design only covered one portfolio at a time — it does now.

---

## Your four questions, answered

**1. Anonymity of company data.** Handle this as a two-stage pipeline, not a one-shot filter: draft (raw, private, editable) → AI sanitization pass on save (strips company names, internal system/product names, and identifying metrics, replacing them with role-accurate substitutes like "a fintech payments platform") → human review/approval before publish. Never expose the raw draft through any public API — only the sanitized version has a public-facing route. Human-in-the-loop matters here for liability: fully automatic redaction will occasionally miss things, and the user (not just the AI) should be the one who signs off before it's public. This is also a marketable feature in its own right — "NDA-safe by design" is a reason other developers would trust the product with real war stories.

**2. Avoiding a LinkedIn feel.** Covered above via IA — but also apply it to tone and visuals: no "profile completeness %", no connection counts, no likes/endorsements. Favor narrative case-study layouts (diagrams, sanitized code snippets, decision trees) over tabular bio fields. The product should read like documentation of real engineering work, not a resume with a photo.

**3. Monetization, given AI isn't free.** A few compatible layers:
- Creator side: freemium — free tier caps sanitization runs/month and storage; paid tier unlocks unlimited sanitization, custom domain, and analytics on who's viewing and what they're asking the AI.
- Recruiter side: free to browse and ask a limited number of AI questions per month per candidate; a paid "recruiter" tier unlocks higher query volume, cross-candidate comparison/search, and saved candidates.
- B2B: license the platform white-label to bootcamps or dev communities as a portfolio-as-a-service — this is the biggest scale lever since it multiplies revenue per AI-cost dollar spent.
- Cost control regardless of tier: cache RAG responses, use cheap embeddings, and keep the default model small (see Q4) — reserve the expensive model for paid tiers only.

**4. Cheap/free model for testing.** Yes — put an LLM-provider abstraction behind the RAG pipeline so the model is a config value, not a code dependency. Dev/test can run on a free or low-cost open-weight model (e.g. via Groq or Together's free tiers) and production can swap to a stronger model per user tier, with zero pipeline changes. This isn't just a testing convenience — it's the same mechanism the tiered monetization model in Q3 needs anyway, so build it once.

**Bonus — recruiter "follow" / subscribe.** Good instinct, but treat it as a feature with real privacy stakes, not just a notification toggle. A recruiter should be able to follow a developer's portfolio and get a digest when a new sprint entry, skill tag, or learning log posts. But the developer needs a visibility control — many users job-hunt quietly while employed, and an accidental "developer just posted new activity" ping to the wrong recruiter is a real risk. Make follow-visibility opt-in per developer, and consider gating "follow + notify" as a paid recruiter feature (ties back into Q3).

---

## Gap closure — features incorporated

Seven additions, in priority order, closing the gaps identified in review. All are reflected in the updated technical architecture doc's schema and pipelines.

**1. Request Interview (the actual north star action).** A persistent CTA next to Ask AI on every portfolio page. Recruiter clicks it, adds a short note, and it creates a tracked request the creator can accept (revealing a scheduling link) or decline. Without this, every other feature builds credibility that has nowhere to go — this is the single most load-bearing addition, since "get recruiters to schedule interviews" was the original goal, not "get recruiters to feel impressed."

**2. Discover — cross-candidate search.** A recruiter-facing surface, separate from any one candidate's portfolio, where recruiters search or ask natural-language questions across every opted-in public portfolio ("show me candidates with real Kubernetes production experience"). Two layers: fast tag/tech faceted filtering as the free baseline, and AI-powered cross-candidate query as the paid-tier feature already planned in monetization. Without this, recruiters can only ever reach a candidate they already had a link to, which quietly breaks the multi-tenant premise.

**3. Guided onboarding for creators.** The blank-page problem kills self-serve products at the empty state. Two assists: (a) import-assisted setup — connect GitHub or upload an existing CV/resume, parsed by AI into draft Experience entries the creator reviews before saving; (b) AI-assisted first sprint entry — a guided prompt ("what did you ship, what was hard, what happened") that helps expand rough notes into the five-part case-study structure, which then goes through the normal sanitize-and-approve pipeline like any other entry.

**4. Trust signal on entries.** Everything in a sprint entry is still self-reported. Lightest-weight fix: an optional "peer-confirmed" badge — the creator can send a one-time confirmation link to a former colleague, who confirms the entry happened without exposing their identity or the employer. Treat this as phase-two, not MVP — it reinforces "evidence, not keywords," but the core product works without it.

**5. Creator analytics.** A private dashboard for the creator: views over time, most-viewed entries, AI questions being asked, and interview requests received. This is what actually tests the core hypothesis (richer narrative → more interviews) rather than just hoping it's true, and it should exist from day one, not as a later add-on.

**6. Visibility controls.** Public (discoverable in search), Unlisted (viewable only via direct link, excluded from Discover), and a "paused" state for creators who want to keep writing privately without being visible to recruiters at all — important for anyone job-hunting quietly while employed. This also determines what "opted-in public" means for Discover, above.

**7. Learning Lab retention nudge.** A monthly diary only works if people keep writing it. A simple scheduled reminder ("it's been a month since your last entry") addresses the habit problem directly rather than assuming discipline will carry it.

---

## Where I'd push back / open questions

**Scope — resolved.** DevLog is a multi-tenant SaaS product: any developer can sign up and configure their own portfolio; recruiters view/query across tenants. This confirms the CLAUDE.md architecture mandate and brings a few requirements forward that a personal-site MVP wouldn't have needed: per-tenant data isolation at the database level, billing/subscription infrastructure for both creator and recruiter tiers, a ToS placing sanitization responsibility on the user (the anonymizer reduces but doesn't eliminate leak risk across many tenants), and a lightweight moderation/report mechanism since you no longer control every published entry personally.

**MVP sequencing.** Don't build everything above at once. A first cut, roughly in priority order: Home, Experience, Sprint Journal, basic Ask-AI, Request Interview, and minimal onboarding (even just a plain form is fine — AI-assisted drafting can come later) plus basic visibility controls (public/unlisted is a launch blocker for anyone currently employed; "paused" state can wait). Discover/cross-candidate search can reasonably wait until there are enough tenants for it to be useful. Learning Lab, trust/verification, creator analytics, and the retention nudge can all follow once you've validated that recruiters actually engage more with sprint case studies than a normal CV. We'll lay this out properly in the MVP phasing pass next.

**Governance, if multi-tenant.** If other users can publish their own sprint logs, the sanitizer reduces but doesn't eliminate leak risk. You'll need a ToS that puts final responsibility on the user, plus a lightweight review/report mechanism.

**Instrumentation.** The core hypothesis is "richer narrative → more interview requests." Instrument for it from day one: time-on-page for sprint entries, AI questions asked per visit, and a trackable "request interview" action — otherwise you won't know if the differentiator is actually working.
