import { notFound } from "next/navigation";
import Link from "next/link";
import { getPortfolioBySlug } from "@/lib/mock-portfolio";
import { Hero } from "@/components/portfolio/Hero";

export default async function HomePage({
  params,
}: {
  params: Promise<{ slug: string }>;
}) {
  const { slug } = await params;
  const portfolio = await getPortfolioBySlug(slug);

  if (!portfolio) {
    notFound();
  }

  const mostRecent = [...portfolio.experiences].sort(
    (a, b) => new Date(b.startDate).getTime() - new Date(a.startDate).getTime()
  )[0];

  return (
    <>
      <Hero portfolio={portfolio} />

      {mostRecent && (
        <section className="mx-auto w-full max-w-4xl px-6 py-12">
          <h2 className="text-sm font-medium uppercase tracking-wide text-zinc-500 dark:text-zinc-400">
            Currently
          </h2>
          <p className="mt-2 text-lg">
            {mostRecent.title} — {mostRecent.companyLabel}
          </p>
          <p className="mt-1 max-w-xl text-zinc-600 dark:text-zinc-400">
            {mostRecent.keyAchievement}
          </p>
          <Link
            href={`/${slug}/experience`}
            className="mt-4 inline-block text-sm font-medium underline underline-offset-2"
          >
            See the full experience timeline →
          </Link>
        </section>
      )}
    </>
  );
}
