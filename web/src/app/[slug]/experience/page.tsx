import { notFound } from "next/navigation";
import { getPortfolioBySlug } from "@/lib/mock-portfolio";
import { ExperienceTimeline } from "@/components/portfolio/ExperienceTimeline";

export default async function ExperiencePage({
  params,
}: {
  params: Promise<{ slug: string }>;
}) {
  const { slug } = await params;
  const portfolio = await getPortfolioBySlug(slug);

  if (!portfolio) {
    notFound();
  }

  return (
    <section className="mx-auto w-full max-w-4xl px-6 py-16">
      <h1 className="text-2xl font-semibold tracking-tight">Experience</h1>
      <p className="mt-1 max-w-xl text-zinc-600 dark:text-zinc-400">
        A streamlined timeline — company, role, tools, and the outcome that
        mattered. Employer names are sanitized descriptors; see Sprint Journal
        for how the work actually happened, once it&apos;s live.
      </p>

      {portfolio.experiences.length === 0 ? (
        <p className="mt-8 text-zinc-500 dark:text-zinc-400">
          Nothing here yet.
        </p>
      ) : (
        <div className="mt-10">
          <ExperienceTimeline experiences={portfolio.experiences} />
        </div>
      )}
    </section>
  );
}
