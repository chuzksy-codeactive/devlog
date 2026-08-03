import { notFound } from "next/navigation";
import Link from "next/link";
import { getPortfolioBySlug } from "@/lib/mock-portfolio";
import { getLearningLogEntriesBySlug } from "@/lib/learning-lab";
import { formatPublishedDate } from "@/lib/portfolio-utils";

// Milestone 17 — Learning Lab, the 5th and last IA surface from the Product
// Brief ("monthly diary + career retrospectives, unified as one growth
// log"). Deliberately identical structure to [slug]/journal/page.tsx — same
// pipeline, same rendering shape, different SourceEntryType and different
// framing copy (retrospective/diary, not case-study).
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
    <section className="mx-auto w-full max-w-4xl px-6 py-16">
      <h1 className="text-3xl font-semibold tracking-tight text-zinc-900">Learning Lab</h1>
      <p className="mt-2 max-w-xl text-zinc-500">
        A running growth log — monthly diary entries and career retrospectives, not case studies. What was
        learned, not just what shipped.
      </p>

      {entries.length === 0 ? (
        <p className="mt-8 text-zinc-400">Nothing published here yet.</p>
      ) : (
        <ol className="mt-10 flex flex-col">
          {entries.map((entry, index) => (
            <li
              key={entry.id}
              className={`py-6 ${index > 0 ? "border-t border-zinc-200" : ""}`}
            >
              <Link href={`/${slug}/learning/${entry.id}`} className="group block">
                <p className="text-xs text-zinc-400">{formatPublishedDate(entry.publishedAt)}</p>
                <h2 className="mt-1.5 text-xl font-semibold text-zinc-900 group-hover:text-indigo-600">
                  {entry.title}
                </h2>
                {entry.tags.length > 0 && (
                  <div className="mt-2.5 flex flex-wrap gap-2">
                    {entry.tags.map((tag) => (
                      <span
                        key={tag}
                        className="rounded-full border border-zinc-200 bg-white px-2.5 py-0.5 text-xs text-zinc-600"
                      >
                        {tag}
                      </span>
                    ))}
                  </div>
                )}
              </Link>
            </li>
          ))}
        </ol>
      )}
    </section>
  );
}
