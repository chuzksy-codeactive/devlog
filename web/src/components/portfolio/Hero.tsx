import { PortfolioData } from "@/lib/types";
import { computeYearsOfExperience } from "@/lib/portfolio-utils";
import { disciplineLabels } from "@/lib/discipline-labels";
import { AskAiForm } from "./AskAiForm";
import { CvDownloadButton } from "./CvDownloadButton";

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
    years !== null ? `${years}+ years of experience` : null;
  const labels = disciplineLabels[portfolio.discipline];

  return (
    <section className="bg-zinc-50 px-6 py-20">
      <div className="mx-auto flex max-w-4xl flex-col gap-10 sm:flex-row sm:items-start">
        <div className="flex h-24 w-24 shrink-0 items-center justify-center rounded-full border border-zinc-200 bg-indigo-50 text-2xl font-semibold text-indigo-600">
          {portfolio.photoUrl ? (
            // eslint-disable-next-line @next/next/no-img-element
            <img
              src={portfolio.photoUrl}
              alt={portfolio.displayName}
              className="h-24 w-24 rounded-full object-cover"
            />
          ) : (
            initials(portfolio.displayName)
          )}
        </div>

        <div className="flex flex-1 flex-col gap-4">
          <div>
            <h1 className="text-3xl font-semibold tracking-tight text-zinc-900">{portfolio.displayName}</h1>
            <p className="mt-1 text-zinc-500">
              {roleLine}
              {yearsLine ? ` · ${yearsLine}` : ""}
            </p>
          </div>

          <p className="max-w-xl text-lg leading-relaxed text-zinc-900">{portfolio.positioningLine}</p>

          {portfolio.coreTechnologies.length > 0 && (
            <div>
              <p className="mb-2 text-xs font-medium uppercase tracking-wide text-zinc-400">
                {labels.coreTechnologiesLabel}
              </p>
              <div className="flex flex-wrap gap-2">
                {portfolio.coreTechnologies.map((tech) => (
                  <span
                    key={tech}
                    className="rounded-full border border-zinc-200 bg-white px-3 py-1 text-xs font-medium text-zinc-600"
                  >
                    {tech}
                  </span>
                ))}
              </div>
            </div>
          )}

          <div className="flex flex-wrap items-center gap-4">
            {portfolio.githubUrl && (
              <a
                href={portfolio.githubUrl}
                target="_blank"
                rel="noopener noreferrer"
                className="inline-flex items-center gap-1.5 text-sm font-medium text-indigo-600 underline underline-offset-2 hover:text-indigo-700"
              >
                GitHub
                {/* Milestone 19 — this is the actual point of verification
                    from a recruiter's side: confirming the link they're
                    about to click really belongs to this candidate, not
                    just that DevLog stored some URL a creator typed in. */}
                {portfolio.githubVerified && (
                  <span className="rounded-full bg-emerald-100 px-1.5 py-0.5 text-[10px] font-medium text-emerald-700">
                    Verified
                  </span>
                )}
              </a>
            )}
            {/* Multi-role expansion — generalized supplement to GitHub above.
                Labels are creator-typed (e.g. "Portfolio site", "Case study"),
                not app-controlled copy, so no discipline-label lookup here. */}
            {portfolio.proofLinks.map((link) => (
              <a
                key={link.url}
                href={link.url}
                target="_blank"
                rel="noopener noreferrer"
                className="text-sm font-medium text-indigo-600 underline underline-offset-2 hover:text-indigo-700"
              >
                {link.label || link.url}
              </a>
            ))}
            {portfolio.hasCv ? <CvDownloadButton slug={portfolio.slug} /> : null}
          </div>

          {/* The AI ask-box embedded directly in the hero, per the Product
              Brief — "this is the hook; don't make them scroll to find it." */}
          <div className="mt-2 max-w-md rounded-2xl border border-zinc-200 bg-white p-5 shadow-sm">
            <p className="mb-3 text-xs font-medium uppercase tracking-wide text-zinc-400">
              Ask about {portfolio.displayName}&apos;s experience
            </p>
            <AskAiForm slug={portfolio.slug} displayName={portfolio.displayName} />
          </div>
        </div>
      </div>
    </section>
  );
}
