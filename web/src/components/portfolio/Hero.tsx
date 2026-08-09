import { PortfolioData } from "@/lib/types";
import { computeYearsOfExperience } from "@/lib/portfolio-utils";
import { disciplineLabels } from "@/lib/discipline-labels";
import { AskAiForm } from "./AskAiForm";
import { CvDownloadButton } from "./CvDownloadButton";

const PROMPT_CHIPS = [
  "What are their top technical strengths?",
  "Any cloud or infrastructure experience?",
  "Show measurable delivery impact.",
  "Any leadership or mentoring experience?",
];

function initials(name: string): string {
  return name
    .split(" ")
    .map((part) => part[0])
    .join("")
    .slice(0, 2)
    .toUpperCase();
}

export function Hero({ portfolio }: { portfolio: PortfolioData }) {
  const years = computeYearsOfExperience(portfolio.experiences);

  const roleLine =
    portfolio.currentRoleTitle ?? "Actively building — open to opportunities";
  const yearsLine =
    years !== null ? `${years}+ years` : null;
  const labels = disciplineLabels[portfolio.discipline];

  return (
    <section className="border-b border-[#e8e5e0] bg-gradient-to-b from-[#faf9f7] to-[#f2f0ec] px-6 py-16">
      <div className="mx-auto max-w-4xl">
        {/* Identity row */}
        <div className="flex flex-col gap-8 sm:flex-row sm:items-start">
          {/* Avatar */}
          <div className="relative shrink-0">
            <div className="flex h-28 w-28 items-center justify-center overflow-hidden rounded-full border-2 border-[#d8d3cc] bg-indigo-50 text-3xl font-semibold text-indigo-600 shadow-lg">
              {portfolio.photoUrl ? (
                // eslint-disable-next-line @next/next/no-img-element
                <img
                  src={portfolio.photoUrl}
                  alt={portfolio.displayName}
                  className="h-full w-full object-cover object-center"
                />
              ) : (
                initials(portfolio.displayName)
              )}
            </div>
            {portfolio.githubVerified && (
              <span className="absolute -bottom-1 -right-1 rounded-full bg-emerald-500 px-2 py-0.5 text-[10px] font-semibold text-white shadow">
                ✓ Verified
              </span>
            )}
          </div>

          {/* Name + proof row */}
          <div className="flex flex-1 flex-col gap-3">
            <div>
              <h1 className="text-4xl font-bold tracking-tight text-zinc-900">{portfolio.displayName}</h1>
              <div className="mt-2 flex flex-wrap items-center gap-3 text-sm text-zinc-500">
                <span className="font-medium text-zinc-800">{roleLine}</span>
                {yearsLine && (
                  <>
                      <span className="text-zinc-400">·</span>
                      <span className="rounded-full bg-indigo-100 px-3 py-0.5 text-xs font-semibold text-indigo-700 shadow-sm">
                      {yearsLine} experience
                    </span>
                  </>
                )}
              </div>
            </div>

            <p className="max-w-2xl text-lg leading-relaxed text-zinc-700">{portfolio.positioningLine}</p>

            {/* Core technologies */}
            {portfolio.coreTechnologies.length > 0 && (
              <div>
                <p className="mb-2 text-xs font-semibold uppercase tracking-widest text-zinc-400">
                  {labels.coreTechnologiesLabel}
                </p>
                <div className="flex flex-wrap gap-2">
                  {portfolio.coreTechnologies.map((tech) => (
                    <span
                      key={tech}
                      className="rounded-full border border-[#dedad4] bg-white px-3 py-1 text-xs font-medium text-zinc-700 shadow-sm"
                    >
                      {tech}
                    </span>
                  ))}
                </div>
              </div>
            )}

            {/* Proof links */}
            <div className="flex flex-wrap items-center gap-3">
              {portfolio.githubUrl && (
                <a
                  href={portfolio.githubUrl}
                  target="_blank"
                  rel="noopener noreferrer"
                  className="inline-flex items-center gap-1.5 rounded-full border border-[#dedad4] bg-white px-4 py-1.5 text-sm font-medium text-zinc-700 shadow-sm transition hover:border-indigo-300 hover:text-indigo-700"
                >
                  GitHub
                  {portfolio.githubVerified && (
                    <span className="rounded-full bg-emerald-100 px-1.5 py-0.5 text-[10px] font-semibold text-emerald-700">
                      Verified
                    </span>
                  )}
                </a>
              )}
              {portfolio.proofLinks.map((link) => (
                <a
                  key={link.url}
                  href={link.url}
                  target="_blank"
                  rel="noopener noreferrer"
                  className="rounded-full border border-[#dedad4] bg-white px-4 py-1.5 text-sm font-medium text-zinc-700 shadow-sm transition hover:border-indigo-300 hover:text-indigo-700"
                >
                  {link.label || link.url}
                </a>
              ))}
              {portfolio.hasCv ? <CvDownloadButton slug={portfolio.slug} /> : null}
            </div>
          </div>
        </div>

        {/* AI recruiter assistant */}
        <div className="mt-10 rounded-2xl border border-indigo-200 bg-gradient-to-br from-indigo-50 to-white p-6 shadow-md ring-1 ring-indigo-100">
          <div className="mb-1 flex items-center gap-2">
            <span className="flex h-5 w-5 items-center justify-center rounded-full bg-indigo-600 text-[10px] font-bold text-white">AI</span>
            <p className="text-sm font-semibold text-zinc-800">
              Ask about {portfolio.displayName}&apos;s experience
            </p>
          </div>
          <p className="mb-4 text-xs text-zinc-500">
            Powered by AI — answers are generated from {portfolio.displayName}&apos;s published portfolio entries only.
          </p>

          {/* Prompt chips */}
          <div className="mb-4 flex flex-wrap gap-2">
            {PROMPT_CHIPS.map((chip) => (
              <span
                key={chip}
                className="cursor-pointer rounded-full border border-indigo-200 bg-white px-3 py-1 text-xs font-medium text-indigo-700 shadow-sm transition hover:bg-indigo-50"
              >
                {chip}
              </span>
            ))}
          </div>

          <AskAiForm slug={portfolio.slug} displayName={portfolio.displayName} />
        </div>
      </div>
    </section>
  );
}
