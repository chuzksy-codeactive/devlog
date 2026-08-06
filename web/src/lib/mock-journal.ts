import { SprintJournalEntryDetail, SprintJournalEntrySummary } from "./types";

const API_BASE_URL = process.env.NEXT_PUBLIC_API_BASE_URL ?? "http://localhost:5000";

export async function getJournalEntriesBySlug(slug: string): Promise<SprintJournalEntrySummary[]> {
  const response = await fetch(`${API_BASE_URL}/api/portfolio/${slug}/entries`, {
    cache: "no-store",
  });

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
