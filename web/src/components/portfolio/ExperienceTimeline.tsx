import { ExperienceEntry, experienceTypeLabel } from "@/lib/types";
import { formatDateRange } from "@/lib/portfolio-utils";

const TYPE_BADGE_STYLES: Record<string, string> = {
  Employer: "bg-blue-50 text-blue-700",
  PersonalProject: "bg-violet-50 text-violet-700",
  OpenSource: "bg-green-50 text-green-700",
  Freelance: "bg-amber-50 text-amber-700",
  Education: "bg-zinc-100 text-zinc-600",
};

export function ExperienceTimeline({ experiences }: { experiences: ExperienceEntry[] }) {
  const sorted = [...experiences].sort(
    (a, b) => new Date(b.startDate).getTime() - new Date(a.startDate).getTime()
  );

  return (
    <ol className="flex flex-col">
      {sorted.map((entry, index) => (
        <li
          key={entry.id}
          className={`border-l-2 border-zinc-200 py-7 pl-6 ${index > 0 ? "border-t border-t-zinc-200" : ""}`}
        >
          <div className="flex flex-wrap items-center gap-2.5">
            <span
              className={`rounded-full px-2.5 py-1 text-xs font-medium ${
                TYPE_BADGE_STYLES[entry.experienceType] ?? TYPE_BADGE_STYLES.Education
              }`}
            >
              {experienceTypeLabel[entry.experienceType]}
            </span>
            <span className="text-sm text-zinc-400">
              {formatDateRange(entry.startDate, entry.endDate)}
            </span>
          </div>

          <h3 className="mt-3 text-lg font-semibold text-zinc-900">{entry.title}</h3>
          <p className="text-zinc-500">{entry.companyLabel}</p>

          {entry.technologies.length > 0 && (
            <div className="mt-3 flex flex-wrap gap-2">
              {entry.technologies.map((tech) => (
                <span
                  key={tech}
                  className="rounded-full border border-zinc-200 bg-white px-2.5 py-0.5 text-xs text-zinc-600"
                >
                  {tech}
                </span>
              ))}
            </div>
          )}

          <p className="mt-3 max-w-2xl text-sm leading-relaxed text-zinc-900">{entry.keyAchievement}</p>

          {/* Forward link into the filtered Sprint Journal for this period —
              Product Brief: "the connective tissue between what I did and how
              I did it survives the split." Sprint Journal page is a later
              milestone, so this is a disabled placeholder for now — kept
              visually muted/disabled rather than accent-colored so it stays
              honest about what's actually clickable. */}
          <span
            className="mt-3 inline-block cursor-not-allowed text-sm text-zinc-400"
            title="Sprint Journal isn't built yet"
          >
            See related sprint entries →
          </span>
        </li>
      ))}
    </ol>
  );
}
