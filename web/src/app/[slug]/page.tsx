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
        // Pinned to the bottom of the viewport (mt-auto in the flex-col
        // `main`) rather than following directly under the Hero -- on tall
        // screens with little content this keeps it anchored as a bottom
        // band instead of floating mid-page. min-h-[38vh] keeps that band
        // substantial (~35-40% of the screen) even when pinned.
        <section className="mt-auto flex w-full min-h-[38vh] flex-col justify-center border-t border-zinc-200 bg-white">
          <div className="mx-auto w-full max-w-4xl px-6 py-12">
            <h2 className="text-xs font-medium uppercase tracking-wide text-zinc-400">
              Currently
            </h2>
            <p className="mt-2 text-xl font-semibold text-zinc-900">
              {mostRecent.title} — {mostRecent.companyLabel}
            </p>
            <p className="mt-1 max-w-xl text-zinc-500">
              {mostRecent.keyAchievement}
            </p>
            <Link
              href={`/${slug}/experience`}
              className="mt-4 inline-block text-sm font-medium text-indigo-600 underline underline-offset-2 hover:text-indigo-700"
            >
              See the full experience timeline →
            </Link>
          </div>
        </section>
      )}
    </>
  );
}
