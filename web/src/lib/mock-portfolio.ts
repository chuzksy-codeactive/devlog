import { PortfolioData } from "./types";

// No longer mock — Milestone 11 wired this to the real
// GET /api/portfolio/:slug endpoint (PortfolioController.GetPublicPortfolio).
// Kept the filename and function signature unchanged deliberately (this
// file's own original comment promised exactly this swap back in
// Milestone 3), so callers in [slug]/layout.tsx, [slug]/page.tsx, and
// [slug]/experience/page.tsx needed zero changes. Runs server-side (every
// caller is a Server Component), so a plain fetch with no CORS concern.
const API_BASE_URL = process.env.NEXT_PUBLIC_API_BASE_URL ?? "http://localhost:5000";

export async function getPortfolioBySlug(slug: string): Promise<PortfolioData | null> {
  const response = await fetch(`${API_BASE_URL}/api/portfolio/${slug}`, {
    // Portfolio/experience data changes via the dashboard at any time —
    // no reason for Next.js's fetch cache to serve a stale copy.
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
