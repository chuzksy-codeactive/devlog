import { notFound } from "next/navigation";
import Link from "next/link";
import { getPortfolioBySlug } from "@/lib/mock-portfolio";
import { getLearningLogEntriesBySlug } from "@/lib/learning-lab";
import { formatPublishedDate } from "@/lib/portfolio-utils";

export default async function LearningLabPage({
  params,
}: {
  params: Promise<{ slug: string }>;
}) {
  const { slug } = await params;
  const [portfolio, entries] = await Promise.all([
    getPortfolioBySlug(slug),
    getLearningLogEntriesBySlug(slug),
  ]);

  if (!portfolio) {
    notFound();
  }

  return (
    <>
      {/* Page header */}
      <section className="border-b border-[#e8e5e0] bg-gradient-to-b from-[#f2f0ec] to-[#faf9f7] px-6 py-12">
        <div className="mx-auto max-w-4xl">
          <p className="text-xs font-semibold uppercase tracking-widest text-indigo-500">Growth log</p>
          <h1 className="mt-2 text-4xl font-bold tracking-tight text-zinc-900">Learning Lab</h1>
          <p className="mt-3 max-w-xl text-base leading-relaxed text-zinc-500">
            A running growth log — monthly diary entries and career retrospectives, not case studies.
            What was learned, not just what shipped.
          </p>
          {entries.length > 0 && (
            <div className="mt-6 flex flex-wrap gap-3">
              <span className="rounded-full border border-[#dedad4] bg-white px-4 py-1.5 text-sm font-medium text-zinc-700 shadow-sm">
                {entries.length} {entries.length === 1 ? "entry" : "entries"} published
              </span>
            </div>
          )}
        </div>
      </section>

      {/* Entry list */}
      <section className="px-6 py-12">
        <div className="mx-auto max-w-4xl">
          {entries.length === 0 ? (
            <div className="rounded-2xl border border-[#e2ded7] bg-white p-10 text-center shadow-sm">
              <p className="text-zinc-400">Nothing published here yet.</p>
            </div>
          ) : (
            <ol className="flex flex-col gap-4">
              {entries.map((entry) => (
                <li key={entry.id}>
                  <Link
                    href={`/${slug}/learning/${entry.id}`}
                    className="group block rounded-2xl border border-[#e2ded7] bg-white p-6 shadow-sm transition hover:border-indigo-200 hover:shadow-md"
                  >
                    <p className="text-xs font-medium text-zinc-400">{formatPublishedDate(entry.publishedAt)}</p>
                    <h2 className="mt-2 text-xl font-bold leading-snug text-zinc-900 transition-colors group-hover:text-indigo-700">
                      {entry.title}
                    </h2>
                    {entry.tags.length > 0 && (
                      <div className="mt-3 flex flex-wrap gap-1.5">
                        {entry.tags.map((tag) => (
                          <span
                            key={tag}
                            className="rounded-full border border-[#dedad4] bg-[#faf9f7] px-2.5 py-0.5 text-xs font-medium text-zinc-600"
                          >
                            {tag}
                          </span>
                        ))}
                      </div>
                    )}
                    <p className="mt-4 text-xs font-semibold text-indigo-600 transition-colors group-hover:text-indigo-800">
                      Read entry →
                    </p>
                  </Link>
                </li>
              ))}
            </ol>
          )}
        </div>
      </section>
    </>
  );
}
