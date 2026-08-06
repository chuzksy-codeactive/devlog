import { LearningLogEntryDetail, LearningLogEntrySummary } from "./types";

const API_BASE_URL = process.env.NEXT_PUBLIC_API_BASE_URL ?? "http://localhost:5000";

export async function getLearningLogEntriesBySlug(slug: string): Promise<LearningLogEntrySummary[]> {
  const response = await fetch(`${API_BASE_URL}/api/portfolio/${slug}/learning`, {
    cache: "no-store",
  });

  if (response.status === 404) {
    return [];
  }

  if (!response.ok) {
    throw new Error(`Failed to load Learning Lab entries for slug "${slug}": ${response.status}`);
  }

  return (await response.json()) as LearningLogEntrySummary[];
}

export async function getLearningLogEntryById(
  slug: string,
  entryId: string
): Promise<LearningLogEntryDetail | null> {
  const response = await fetch(`${API_BASE_URL}/api/portfolio/${slug}/learning/${entryId}`, {
    cache: "no-store",
  });

  if (response.status === 404) {
    return null;
  }

  if (!response.ok) {
    throw new Error(`Failed to load Learning Lab entry "${entryId}" for slug "${slug}": ${response.status}`);
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
