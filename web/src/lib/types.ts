// Mirrors DevLog.Domain.Entities.ExperienceType (api/DevLog.Domain/Entities/Enums.cs).
// Kept in sync by hand until the OpenAPI-generated TS client exists —
// see DevLog-Technical-Architecture.md Section 5's codegen note.
export type ExperienceType =
  | "Employer"
  | "PersonalProject"
  | "OpenSource"
  | "Freelance"
  | "Education";

export interface ExperienceEntry {
  id: string;
  experienceType: ExperienceType;
  // Sanitized descriptor for Employer rows ("a Series B fintech, payments
  // team"), or a short project label for everything else — never a real
  // employer name. See Section 3's anonymization pipeline.
  companyLabel: string;
  title: string;
  startDate: string; // ISO date (yyyy-MM-dd)
  endDate: string | null; // null = present
  technologies: string[];
  keyAchievement: string;
}

export interface PortfolioData {
  slug: string;
  displayName: string;
  // One-line positioning statement for the hero — not a generic "About me."
  positioningLine: string;
  // Null is a real, designed-for state (Product Brief: "graceful
  // non-employed state") — render as "Actively building" / "Aspiring
  // [role]" instead of a title, never as an empty string.
  currentRoleTitle: string | null;
  coreTechnologies: string[];
  githubUrl: string | null;
  cvUrl: string | null;
  photoUrl: string | null;
  experiences: ExperienceEntry[];
}

export const experienceTypeLabel: Record<ExperienceType, string> = {
  Employer: "Employer",
  PersonalProject: "Personal Project",
  OpenSource: "Open Source",
  Freelance: "Freelance",
  Education: "Education",
};
