import { notFound } from "next/navigation";
import Link from "next/link";
import { getPortfolioBySlug } from "@/lib/mock-portfolio";
import { getLearningLogEntryById } from "@/lib/learning-lab";
import { formatPublishedDate } from "@/lib/portfolio-utils";
import { SanitizedMarkdown } from "@/components/portfolio/SanitizedMarkdown";

export default async function LearningLabEntryPage({
  params,
}: {
  params: Promise<{ slug: string; entryId: string }>;
}) {
  const { slug, entryId } = await params;
  const [portfolio, entry] = await Promise.all([
    getPortfolioBySlug(slug),
    getLearningLogEntryById(slug, entryId),
  ]);

  if (!portfolio || !entry) {
    notFound();
  }

  return (
    <article className="mx-auto w-full max-w-3xl px-6 py-16">
      <Link href={`/${slug}/learning`} className="text-sm text-zinc-500 hover:text-zinc-900">
        ← Learning Lab
      </Link>

      <p className="mt-6 text-sm text-zinc-400">{formatPublishedDate(entry.publishedAt)}</p>
      <h1 className="mt-1.5 text-3xl font-semibold tracking-tight text-zinc-900">{entry.title}</h1>

      {entry.tags.length > 0 && (
        <div className="mt-3 flex flex-wrap gap-2">
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

      <div className="mt-10">
        <SanitizedMarkdown content={entry.content} />
      </div>
    </article>
  );
}
