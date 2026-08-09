import { notFound } from "next/navigation";
import Link from "next/link";
import { getPortfolioBySlug } from "@/lib/mock-portfolio";
import { getJournalEntryById } from "@/lib/mock-journal";
import { formatPublishedDate } from "@/lib/portfolio-utils";
import { SanitizedMarkdown } from "@/components/portfolio/SanitizedMarkdown";

export default async function SprintJournalEntryPage({
  params,
}: {
  params: Promise<{ slug: string; entryId: string }>;
}) {
  const { slug, entryId } = await params;
  const [portfolio, entry] = await Promise.all([
    getPortfolioBySlug(slug),
    getJournalEntryById(slug, entryId),
  ]);

  if (!portfolio || !entry) {
    notFound();
  }

  return (
    <>
      {/* Article header */}
      <section className="border-b border-[#e8e5e0] bg-gradient-to-b from-[#f2f0ec] to-[#faf9f7] px-6 py-12">
        <div className="mx-auto max-w-3xl">
          <Link
            href={`/${slug}/journal`}
            className="inline-flex items-center gap-1 text-xs font-semibold text-indigo-600 transition hover:text-indigo-800"
          >
            ← Sprint Journal
          </Link>

          <p className="mt-6 text-xs font-medium text-zinc-400">{formatPublishedDate(entry.publishedAt)}</p>
          <h1 className="mt-2 text-4xl font-bold tracking-tight text-zinc-900 leading-tight">
            {entry.title}
          </h1>

          {entry.tags.length > 0 && (
            <div className="mt-4 flex flex-wrap gap-1.5">
              {entry.tags.map((tag) => (
                <span
                  key={tag}
                  className="rounded-full border border-[#dedad4] bg-white px-3 py-1 text-xs font-medium text-zinc-600 shadow-sm"
                >
                  {tag}
                </span>
              ))}
            </div>
          )}
        </div>
      </section>

      {/* Article body */}
      <section className="px-6 py-12">
        <div className="mx-auto max-w-3xl">
          <div className="rounded-2xl border border-[#e2ded7] bg-white p-8 shadow-sm">
            <SanitizedMarkdown content={entry.content} />
          </div>

          {/* Footer nav */}
          <div className="mt-8 flex items-center justify-between">
            <Link
              href={`/${slug}/journal`}
              className="inline-flex items-center gap-1 text-sm font-semibold text-indigo-600 transition hover:text-indigo-800"
            >
              ← Back to Sprint Journal
            </Link>
            <Link
              href={`/${slug}/experience`}
              className="text-sm font-medium text-zinc-500 transition hover:text-zinc-800"
            >
              View experience timeline →
            </Link>
          </div>
        </div>
      </section>
    </>
  );
}
