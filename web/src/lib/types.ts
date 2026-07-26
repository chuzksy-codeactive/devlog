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

// Mirrors GET /api/portfolio/:slug/entries — deliberately without `content`,
// matching the backend's listing/detail split (SprintJournalController.cs).
export interface SprintJournalEntrySummary {
  id: string;
  title: string;
  tags: string[];
  publishedAt: string; // ISO datetime
}

// Mirrors GET /api/portfolio/:slug/entries/:id — adds the full sanitized
// markdown body, rendered only through SanitizedMarkdown.tsx, never
// dangerouslySetInnerHTML directly (architecture doc Section 9's stored-XSS
// fix).
export interface SprintJournalEntryDetail extends SprintJournalEntrySummary {
  content: string;
}

export type PortfolioVisibility = "Public" | "Unlisted" | "Paused";

// Mirrors GET/PUT /api/portfolio/me (PortfolioController) — the
// creator-authenticated shape, a superset of the public PortfolioData
// (adds Visibility/Discoverable/NotificationEmail, which are never exposed
// publicly).
export interface MyPortfolio {
  slug: string;
  headline: string;
  displayName: string;
  positioningLine: string;
  photoUrl: string | null;
  currentRole: string | null;
  yearsExperience: string | null;
  coreTechnologies: string[];
  linkedInUrl: string | null;
  gitHubUrl: string | null;
  cvFileUrl: string | null;
  visibility: PortfolioVisibility;
  discoverable: boolean;
  notificationEmail: string | null;
}

// Mirrors ExperienceController's DTO shape (GET /api/experiences/me, etc.)
export interface MyExperience {
  id: string;
  experienceType: ExperienceType;
  companyLabel: string;
  title: string;
  startDate: string; // yyyy-MM-dd
  endDate: string | null;
  technologies: string[];
  keyAchievement: string;
}

// Mirrors EntriesController's DTO shape (GET /api/entries, etc.)
export type RawEntryStatus = "Draft" | "Sanitizing" | "PendingApproval" | "Approved" | "Rejected";
export type RawEntryType = "SprintEntry" | "Experience" | "LearningLog";

export interface MyEntry {
  id: string;
  entryType: RawEntryType;
  title: string;
  rawContent: string;
  sanitizedPreview: string | null;
  sanitizationModelVersion: string | null;
  status: RawEntryStatus;
  createdAt: string;
}

// Mirrors CreatorInterviewRequestsController's DTO shape.
export type InterviewRequestStatus = "Pending" | "Accepted" | "Declined";

export interface MyInterviewRequest {
  id: string;
  recruiterEmail: string;
  recruiterName: string | null;
  note: string;
  status: InterviewRequestStatus;
  contactInstructions: string | null;
  createdAt: string;
  respondedAt: string | null;
}
