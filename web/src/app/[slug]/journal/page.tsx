import { notFound } from "next/navigation";
import Link from "next/link";
import { getPortfolioBySlug } from "@/lib/mock-portfolio";
import { getJournalEntriesBySlug } from "@/lib/mock-journal";
import { formatPublishedDate } from "@/lib/portfolio-utils";

export default async function SprintJournalPage({
  params,
}: {
  params: Promise<{ slug: string }>;
}) {
  const { slug } = await params;
  const [portfolio, entries] = await Promise.all([
    getPortfolioBySlug(slug),
    getJournalEntriesBySlug(slug),
  ]);

  if (!portfolio) {
    notFound();
  }

  return (
    <section className="mx-auto w-full max-w-4xl px-6 py-16">
      <h1 className="text-3xl font-semibold tracking-tight text-zinc-900">Sprint Journal</h1>
      <p className="mt-2 max-w-xl text-zinc-500">
        Case studies of real work — what shipped, what was hard, and how it
        actually happened. Anonymized: no real employer or product names.
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
              <Link href={`/${slug}/journal/${entry.id}`} className="group block">
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
