import Link from "next/link";
import { ExperienceEntry, experienceTypeLabel } from "@/lib/types";
import { formatDateRange } from "@/lib/portfolio-utils";

const TYPE_BADGE_STYLES: Record<string, string> = {
  Employer: "bg-indigo-50 text-indigo-700 border-indigo-200",
  PersonalProject: "bg-violet-50 text-violet-700 border-violet-200",
  OpenSource: "bg-emerald-50 text-emerald-700 border-emerald-200",
  Freelance: "bg-amber-50 text-amber-700 border-amber-200",
  Education: "bg-zinc-100 text-zinc-600 border-zinc-200",
};

export function ExperienceTimeline({ experiences, slug }: { experiences: ExperienceEntry[]; slug: string }) {
  const sorted = [...experiences].sort(
    (a, b) => new Date(b.startDate).getTime() - new Date(a.startDate).getTime()
  );

  return (
    <ol className="flex flex-col gap-4">
      {sorted.map((entry) => (
        <li
          key={entry.id}
          className="rounded-2xl border border-[#e2ded7] bg-white p-6 shadow-sm transition hover:border-indigo-200 hover:shadow-md"
        >
          {/* Type badge + date */}
          <div className="flex flex-wrap items-center gap-2.5">
            <span
              className={`rounded-full border px-2.5 py-0.5 text-xs font-semibold ${
                TYPE_BADGE_STYLES[entry.experienceType] ?? TYPE_BADGE_STYLES.Education
              }`}
            >
              {experienceTypeLabel[entry.experienceType]}
            </span>
            <span className="text-xs font-medium text-zinc-400">
              {formatDateRange(entry.startDate, entry.endDate)}
            </span>
          </div>

          {/* Role + company */}
          <h3 className="mt-3 text-lg font-bold leading-snug text-zinc-900">{entry.title}</h3>
          <p className="mt-0.5 text-sm text-zinc-500">{entry.companyLabel}</p>

          {/* Tech stack */}
          {entry.technologies.length > 0 && (
            <div className="mt-4 flex flex-wrap gap-1.5">
              {entry.technologies.map((tech) => (
                <span
                  key={tech}
                  className="rounded-full border border-[#dedad4] bg-[#faf9f7] px-2.5 py-0.5 text-xs font-medium text-zinc-600"
                >
                  {tech}
                </span>
              ))}
            </div>
          )}

          {/* Key achievement */}
          <p className="mt-4 max-w-2xl text-sm leading-relaxed text-zinc-700">{entry.keyAchievement}</p>

          {/* Sprint Journal link */}
          <div className="mt-4 border-t border-[#ede9e3] pt-4">
            <Link
              href={`/${slug}/journal`}
              className="inline-flex items-center gap-1 text-xs font-semibold text-indigo-600 transition hover:text-indigo-800"
            >
              See sprint entries →
            </Link>
          </div>
        </li>
      ))}
    </ol>
  );
}
