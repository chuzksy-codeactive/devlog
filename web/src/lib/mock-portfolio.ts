import { PortfolioData } from "./types";

const API_BASE_URL = process.env.NEXT_PUBLIC_API_BASE_URL ?? "http://localhost:5000";

export async function getPortfolioBySlug(slug: string): Promise<PortfolioData | null> {
  const response = await fetch(`${API_BASE_URL}/api/portfolio/${slug}`, {
    cache: "no-store",
  });

  if (response.status === 404) {
    return null;
  }

  if (!response.ok) {
    throw new Error(`Failed to load portfolio for slug "${slug}": ${response.status}`);
  }

  return (await response.json()) as PortfolioData;
}
