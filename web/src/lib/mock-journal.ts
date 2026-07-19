import { SprintJournalEntryDetail, SprintJournalEntrySummary } from "./types";

// Stand-in for GET /api/portfolio/:slug/entries[/:id] (SprintJournalController.cs,
// Milestone 9), same "swap for fetch() later" pattern as mock-portfolio.ts.
// Content here is intentionally markdown, including a fenced code block and
// a link — the point of these two fixtures existing is to exercise
// SanitizedMarkdown's actual rendering path (headings, lists, code, links),
// not just placeholder text.
const MOCK_ENTRIES: Record<string, SprintJournalEntryDetail[]> = {
  chika: [
    {
      id: "entry-1",
      title: "Fixing a cross-tenant data leak nobody had caught yet",
      tags: ["PostgreSQL", "Row-Level Security", ".NET"],
      publishedAt: "2026-07-11T18:30:00Z",
      content: `## Problem

A portfolio-read endpoint had no explicit tenant filter, relying entirely on Postgres Row-Level Security to scope results. That's normally fine — except the table it queried had never actually been given an RLS policy, so with more than one tenant in the database it could return a different tenant's private data.

## Solution

Added the missing \`tenant_isolation\` and \`public_read\` policies, then added an explicit application-layer filter on top — defense in depth, not either/or.

\`\`\`sql
CREATE POLICY tenant_isolation ON portfolios
  USING (tenant_id = current_setting('app.current_tenant', true)::uuid);
\`\`\`

## Technical design

- Two independent layers: RLS at the database, an explicit \`WHERE\` clause in the query. Neither one alone was enough.
- Traced the bug by asking "what actually enforces this claim in the code comment," not by trusting the comment.

## Challenges

The fix surfaced a second dependency: the role the app connects as turned out to be a Postgres superuser, which bypasses RLS unconditionally regardless of policy correctness. See the next entry.

## Result

Zero cross-tenant reads possible now, proven by an isolation test that (for the first time) actually runs against a non-superuser role.`,
    },
    {
      id: "entry-2",
      title: "The app was connecting as a superuser the whole time",
      tags: ["PostgreSQL", "Security"],
      publishedAt: "2026-07-11T20:15:00Z",
      content: `## Problem

Every Row-Level Security policy in the schema carried the same note: "needs a non-owner role + \`FORCE ROW LEVEL SECURITY\`." That note was aiming at the wrong thing.

## Solution

Checked how the database role was actually created. \`POSTGRES_USER\` in the Docker Postgres image is provisioned as a genuine superuser — and Postgres disables row security unconditionally for superusers. \`FORCE ROW LEVEL SECURITY\` only affects a non-superuser *owner*; it does nothing for an actual superuser.

The real fix: a dedicated, unprivileged role for the running application, completely separate from the role that owns the tables and runs migrations.

## Result

RLS policies are now enforced against real application traffic for the first time since the schema was designed — not just internally consistent SQL that nothing was actually checking.`,
    },
  ],
};

export async function getJournalEntriesBySlug(slug: string): Promise<SprintJournalEntrySummary[]> {
  const entries = MOCK_ENTRIES[slug] ?? [];
  return entries
    .map(({ id, title, tags, publishedAt }) => ({ id, title, tags, publishedAt }))
    .sort((a, b) => new Date(b.publishedAt).getTime() - new Date(a.publishedAt).getTime());
}

export async function getJournalEntryById(
  slug: string,
  entryId: string
): Promise<SprintJournalEntryDetail | null> {
  const entries = MOCK_ENTRIES[slug] ?? [];
  return entries.find((entry) => entry.id === entryId) ?? null;
}
