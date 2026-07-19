import { ExperienceEntry } from "./types";

// Years of experience is derived, not stored — computed from the earliest
// Employer-type experience's start date to now (or to that role's end date
// if none are still current). Deliberately excludes PersonalProject/
// Education/OpenSource/Freelance from this specific stat so "years of
// experience" keeps its conventional recruiter-facing meaning; those types
// still show up in the timeline itself.
export function computeYearsOfExperience(experiences: ExperienceEntry[]): number | null {
  const employerSpans = experiences.filter((e) => e.experienceType === "Employer");
  if (employerSpans.length === 0) {
    return null;
  }

  const earliestStart = employerSpans
    .map((e) => new Date(e.startDate).getTime())
    .reduce((min, t) => Math.min(min, t));

  const years = (Date.now() - earliestStart) / (1000 * 60 * 60 * 24 * 365.25);
  return Math.max(0, Math.floor(years * 10) / 10);
}

export function formatDateRange(startDate: string, endDate: string | null): string {
  const start = new Date(startDate);
  const startLabel = start.toLocaleDateString("en-US", { month: "short", year: "numeric" });

  if (endDate === null) {
    return `${startLabel} — Present`;
  }

  const end = new Date(endDate);
  const endLabel = end.toLocaleDateString("en-US", { month: "short", year: "numeric" });
  return `${startLabel} — ${endLabel}`;
}

export function formatPublishedDate(isoDateTime: string): string {
  return new Date(isoDateTime).toLocaleDateString("en-US", {
    month: "long",
    day: "numeric",
    year: "numeric",
  });
}
