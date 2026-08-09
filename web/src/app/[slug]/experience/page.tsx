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

  const count = portfolio.experiences.length;
  const employers = portfolio.experiences.filter((e) => e.experienceType === "Employer").length;

  return (
    <>
      {/* Page header */}
      <section className="border-b border-[#e8e5e0] bg-gradient-to-b from-[#f2f0ec] to-[#faf9f7] px-6 py-12">
        <div className="mx-auto max-w-4xl">
          <p className="text-xs font-semibold uppercase tracking-widest text-indigo-500">Career timeline</p>
          <h1 className="mt-2 text-4xl font-bold tracking-tight text-zinc-900">Experience</h1>
          <p className="mt-3 max-w-xl text-base leading-relaxed text-zinc-500">
            A streamlined timeline — company, role, tools, and the outcome that mattered.
            Employer names are sanitized descriptors; see Sprint Journal for how the work
            actually happened.
          </p>
          {count > 0 && (
            <div className="mt-6 flex flex-wrap gap-3">
              <span className="rounded-full border border-[#dedad4] bg-white px-4 py-1.5 text-sm font-medium text-zinc-700 shadow-sm">
                {count} {count === 1 ? "entry" : "entries"}
              </span>
              {employers > 0 && (
                <span className="rounded-full border border-indigo-200 bg-indigo-50 px-4 py-1.5 text-sm font-medium text-indigo-700 shadow-sm">
                  {employers} employer {employers === 1 ? "role" : "roles"}
                </span>
              )}
            </div>
          )}
        </div>
      </section>

      {/* Timeline */}
      <section className="px-6 py-12">
        <div className="mx-auto max-w-4xl">
          {portfolio.experiences.length === 0 ? (
            <div className="rounded-2xl border border-[#e2ded7] bg-white p-10 text-center shadow-sm">
              <p className="text-zinc-400">No experience entries yet.</p>
            </div>
          ) : (
            <ExperienceTimeline experiences={portfolio.experiences} slug={slug} />
          )}
        </div>
      </section>
    </>
  );
}
