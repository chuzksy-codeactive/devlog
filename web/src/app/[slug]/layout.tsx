import { notFound } from "next/navigation";
import { getPortfolioBySlug } from "@/lib/mock-portfolio";
import { PortfolioNav } from "@/components/portfolio/PortfolioNav";
import { AskAiWidget } from "@/components/portfolio/AskAiWidget";

// Shared shell for every public-portfolio surface (Home, Experience, Sprint
// Journal, Learning Lab) — the persistent nav, Ask AI widget, and Request
// Interview CTA all live here so they survive navigation between those
// pages, per the Product Brief's 5-surface IA. Real data source is
// getPortfolioBySlug (currently mock; swap for a fetch to
// GET /api/portfolio/:slug once that endpoint exists).
export default async function PortfolioLayout({
  children,
  params,
}: {
  children: React.ReactNode;
  params: Promise<{ slug: string }>;
}) {
  const { slug } = await params;
  const portfolio = await getPortfolioBySlug(slug);

  if (!portfolio) {
    notFound();
  }

  return (
    <div className="flex min-h-full flex-1 flex-col bg-white text-zinc-900">
      <PortfolioNav slug={slug} displayName={portfolio.displayName} />
      <main className="flex flex-1 flex-col">{children}</main>
      <AskAiWidget slug={slug} displayName={portfolio.displayName} />
    </div>
  );
}
