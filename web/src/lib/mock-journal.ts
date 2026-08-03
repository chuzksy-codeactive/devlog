import { SprintJournalEntryDetail, SprintJournalEntrySummary } from "./types";

// No longer mock — wired to the real GET /api/portfolio/:slug/entries[/:id]
// endpoints (SprintJournalController.cs). This file's own original comment,
// since Milestone 9, promised exactly this "swap for fetch() later" — it
// never actually happened until now, unlike mock-portfolio.ts's equivalent
// swap in Milestone 11. Found because a real published Sprint Journal entry
// wasn't showing up on the public /journal page: the page was still reading
// hardcoded fixture data keyed to the "chika" slug specifically, so any
// other slug (including every real auto-provisioned one) always got back an
// empty list, regardless of what was actually published in the database.
// Kept the filename and function signatures unchanged, same reasoning as
// mock-portfolio.ts, so [slug]/journal/page.tsx and
// [slug]/journal/[entryId]/page.tsx needed zero changes. Runs server-side
// (both callers are Server Components), so a plain fetch with no CORS
// concern.
const API_BASE_URL = process.env.NEXT_PUBLIC_API_BASE_URL ?? "http://localhost:5000";

export async function getJournalEntriesBySlug(slug: string): Promise<SprintJournalEntrySummary[]> {
  const response = await fetch(`${API_BASE_URL}/api/portfolio/${slug}/entries`, {
    // Same reasoning as mock-portfolio.ts: entries publish via the
    // dashboard at any time, never serve a stale cached list.
    cache: "no-store",
  });

  // A missing/paused portfolio 404s here too (SprintJournalController
  // re-checks visibility independently) — treated as "no entries" rather
  // than thrown, since the page's own getPortfolioBySlug call already
  // handles the "portfolio doesn't exist" case via notFound().
  if (response.status === 404) {
    return [];
  }

  if (!response.ok) {
    throw new Error(`Failed to load journal entries for slug "${slug}": ${response.status}`);
  }

  return (await response.json()) as SprintJournalEntrySummary[];
}

export async function getJournalEntryById(
  slug: string,
  entryId: string
): Promise<SprintJournalEntryDetail | null> {
  const response = await fetch(`${API_BASE_URL}/api/portfolio/${slug}/entries/${entryId}`, {
    cache: "no-store",
  });

  if (response.status === 404) {
    return null;
  }

  if (!response.ok) {
    throw new Error(`Failed to load journal entry "${entryId}" for slug "${slug}": ${response.status}`);
  }

  // Backend field is sanitizedContent (SprintJournalController's detail
  // shape), not content — mapped here rather than renaming the frontend
  // type, since SanitizedMarkdown and every caller already expect `content`.
  const dto = (await response.json()) as {
    id: string;
    title: string;
    sanitizedContent: string;
    tags: string[];
    publishedAt: string;
  };

  return {
    id: dto.id,
    title: dto.title,
    tags: dto.tags,
    publishedAt: dto.publishedAt,
    content: dto.sanitizedContent,
  };
}
