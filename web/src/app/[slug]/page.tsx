import { notFound } from "next/navigation";
import Link from "next/link";
import { getPortfolioBySlug } from "@/lib/mock-portfolio";
import { Hero } from "@/components/portfolio/Hero";
import { formatDateRange } from "@/lib/portfolio-utils";

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

  const sorted = [...portfolio.experiences].sort(
    (a, b) => new Date(b.startDate).getTime() - new Date(a.startDate).getTime()
  );
  const mostRecent = sorted[0];
  const highlights = sorted.slice(0, 3);

  return (
    <>
      <Hero portfolio={portfolio} />

      {/* Impact highlights */}
      {highlights.length > 0 && (
        <section className="border-b border-[#e8e5e0] bg-[#faf9f7] px-6 py-14">
          <div className="mx-auto max-w-4xl">
            <h2 className="text-xs font-semibold uppercase tracking-widest text-zinc-400">
              Recent impact
            </h2>
            <p className="mt-1 text-sm text-zinc-500">
              A snapshot of delivery moments from {portfolio.displayName}&apos;s career.
            </p>
            <div className="mt-6 grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
              {highlights.map((exp) => (
                <div
                  key={exp.id}
                  className="rounded-2xl border border-[#e2ded7] bg-white p-5 shadow-sm transition hover:border-indigo-200 hover:shadow-md"
                >
                  <p className="text-xs font-semibold uppercase tracking-widest text-indigo-500">
                    {exp.experienceType === "Employer" ? "Employer" : exp.experienceType}
                  </p>
                  <p className="mt-2 font-semibold leading-snug text-zinc-900">
                    {exp.title}
                    <span className="block text-sm font-normal text-zinc-500">{exp.companyLabel}</span>
                  </p>
                  <p className="mt-1 text-xs text-zinc-400">{formatDateRange(exp.startDate, exp.endDate)}</p>
                  <p className="mt-3 text-sm leading-relaxed text-zinc-700">{exp.keyAchievement}</p>
                  {exp.technologies.length > 0 && (
                    <div className="mt-3 flex flex-wrap gap-1.5">
                      {exp.technologies.slice(0, 4).map((t) => (
                        <span key={t} className="rounded-full border border-[#dedad4] bg-[#faf9f7] px-2 py-0.5 text-xs text-zinc-600">
                          {t}
                        </span>
                      ))}
                      {exp.technologies.length > 4 && (
                        <span className="rounded-full border border-[#dedad4] bg-[#faf9f7] px-2 py-0.5 text-xs text-zinc-400">
                          +{exp.technologies.length - 4}
                        </span>
                      )}
                    </div>
                  )}
                </div>
              ))}
            </div>
            <div className="mt-6">
              <Link
                href={`/${slug}/experience`}
                className="inline-flex items-center gap-1 text-sm font-semibold text-indigo-600 underline underline-offset-2 hover:text-indigo-700"
              >
                See the full experience timeline →
              </Link>
            </div>
          </div>
        </section>
      )}

      {/* Current role */}
      {mostRecent && (
        <section className="bg-gradient-to-b from-[#f2f0ec] to-[#ebe8e3] px-6 py-14">
          <div className="mx-auto max-w-4xl">
            <h2 className="text-xs font-semibold uppercase tracking-widest text-zinc-400">
              Currently
            </h2>
            <p className="mt-2 text-2xl font-bold text-zinc-900">
              {mostRecent.title}
              <span className="block text-lg font-normal text-zinc-500">{mostRecent.companyLabel}</span>
            </p>
            <p className="mt-3 max-w-2xl text-base leading-relaxed text-zinc-600">
              {mostRecent.keyAchievement}
            </p>
          </div>
        </section>
      )}
    </>
  );
}
