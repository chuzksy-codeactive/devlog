import { ExperienceEntry, experienceTypeLabel } from "@/lib/types";
import { formatDateRange } from "@/lib/portfolio-utils";

const TYPE_BADGE_STYLES: Record<string, string> = {
  Employer: "bg-blue-50 text-blue-700 dark:bg-blue-950 dark:text-blue-300",
  PersonalProject: "bg-purple-50 text-purple-700 dark:bg-purple-950 dark:text-purple-300",
  OpenSource: "bg-green-50 text-green-700 dark:bg-green-950 dark:text-green-300",
  Freelance: "bg-amber-50 text-amber-700 dark:bg-amber-950 dark:text-amber-300",
  Education: "bg-zinc-100 text-zinc-700 dark:bg-zinc-800 dark:text-zinc-300",
};

export function ExperienceTimeline({ experiences }: { experiences: ExperienceEntry[] }) {
  const sorted = [...experiences].sort(
    (a, b) => new Date(b.startDate).getTime() - new Date(a.startDate).getTime()
  );

  return (
    <ol className="flex flex-col gap-8">
      {sorted.map((entry) => (
        <li key={entry.id} className="relative border-l border-black/10 pl-6 dark:border-white/10">
          <div className="absolute -left-[5px] top-1.5 h-2.5 w-2.5 rounded-full bg-black dark:bg-white" />

          <div className="flex flex-wrap items-center gap-2">
            <span
              className={`rounded-full px-2.5 py-0.5 text-xs font-medium ${
                TYPE_BADGE_STYLES[entry.experienceType] ?? TYPE_BADGE_STYLES.Education
              }`}
            >
              {experienceTypeLabel[entry.experienceType]}
            </span>
            <span className="text-sm text-zinc-500 dark:text-zinc-400">
              {formatDateRange(entry.startDate, entry.endDate)}
            </span>
          </div>

          <h3 className="mt-2 text-lg font-medium">{entry.title}</h3>
          <p className="text-zinc-600 dark:text-zinc-400">{entry.companyLabel}</p>

          {entry.technologies.length > 0 && (
            <div className="mt-3 flex flex-wrap gap-2">
              {entry.technologies.map((tech) => (
                <span
                  key={tech}
                  className="rounded-full bg-zinc-100 px-2.5 py-0.5 text-xs text-zinc-700 dark:bg-zinc-800 dark:text-zinc-300"
                >
                  {tech}
                </span>
              ))}
            </div>
          )}

          <p className="mt-3 max-w-2xl text-sm leading-relaxed">{entry.keyAchievement}</p>

          {/* Forward link into the filtered Sprint Journal for this period —
              Product Brief: "the connective tissue between what I did and how
              I did it survives the split." Sprint Journal page is a later
              milestone, so this is a disabled placeholder for now. */}
          <span
            className="mt-3 inline-block cursor-not-allowed text-sm text-zinc-400 dark:text-zinc-600"
            title="Sprint Journal isn't built yet"
          >
            See related sprint entries →
          </span>
        </li>
      ))}
    </ol>
  );
}
