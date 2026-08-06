import { notFound } from "next/navigation";
import { getPortfolioBySlug } from "@/lib/mock-portfolio";
import { PortfolioNav } from "@/components/portfolio/PortfolioNav";
import { AskAiWidget } from "@/components/portfolio/AskAiWidget";

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
