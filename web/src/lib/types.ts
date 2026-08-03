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

// Mirrors DevLog.Domain.Entities.PortfolioDiscipline (Enums.cs) — multi-role
// expansion. Purely a label-selection key (see discipline-labels.ts); no
// frontend logic ever branches behaviorally on it.
export type Discipline =
  | "SoftwareEngineer"
  | "ProductManager"
  | "BusinessAnalyst"
  | "DevOps"
  | "ScrumMaster"
  | "Other";

// Mirrors DevLog.Domain.Entities.ProofLink — one entry in a Portfolio's
// generalized ProofLinks list (supplement to the fixed githubUrl/cvUrl
// fields below).
export interface ProofLink {
  label: string;
  url: string;
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
  discipline: Discipline;
  proofLinks: ProofLink[];
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
  discipline: Discipline;
  proofLinks: ProofLink[];
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

// Mirrors OnboardingController's GitHub-import and resume-import response
// shapes (OnboardingImportService.SuggestFromGitHubAsync /
// SuggestFromResumeAsync). Nothing here is saved yet — these are
// suggestions the creator reviews and explicitly accepts, one at a time,
// through the normal /api/experiences and /api/entries endpoints.
// repoUrl is null for resume-sourced suggestions (Milestone 13b) — there's
// no repo to link to.
export interface SuggestedExperience {
  experienceType: ExperienceType;
  companyLabel: string;
  title: string;
  technologies: string[];
  keyAchievement: string;
  startDate: string; // yyyy-MM-dd
  endDate: string | null; // yyyy-MM-dd
  repoUrl: string | null;
}

export interface SuggestedEntry {
  title: string;
  rawContent: string;
}

export interface OnboardingSuggestions {
  experiences: SuggestedExperience[];
  draftEntry: SuggestedEntry | null;
}

// Mirrors POST /api/onboarding/resume-import's response shape
// (OnboardingController.ResumeImport). cvFileUrl is always populated on a
// 200 — the file is stored regardless of whether suggestions could be
// drafted from it. suggestions/message are mutually exclusive: a scanned
// or image-only PDF returns message instead of suggestions.
export interface ResumeImportResponse {
  cvFileUrl: string;
  suggestions: OnboardingSuggestions | null;
  message?: string;
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

// Mirrors GET /api/analytics/summary (AnalyticsController, Milestone 16).
// Read-only aggregation over data that's been logging since Milestone 5
// (interview_requests) and Milestone 12 (portfolio_view_events,
// ai_query_logs) — this is the first UI ever built on top of it.
export interface DailyCount {
  date: string; // yyyy-MM-dd
  count: number;
}

export interface AnalyticsSummary {
  viewsTotal: number;
  views7d: number;
  views30d: number;
  queriesTotal: number;
  queries7d: number;
  queries30d: number;
  interviewRequestsVerified: number;
  interviewRequestsPending: number;
  interviewRequestsAccepted: number;
  viewsByDay: DailyCount[];
  queriesByDay: DailyCount[];
}

// Mirrors GET /api/portfolio/:slug/learning[/:id] (LearningLabController,
// Milestone 17) — identical shape to the Sprint Journal entry types, since
// both surfaces are the same raw_entries -> sanitize -> approve ->
// published_entries pipeline filtered by a different SourceEntryType.
// Aliased rather than duplicated so the two surfaces can't silently drift
// apart in shape without a compiler error.
export type LearningLogEntrySummary = SprintJournalEntrySummary;
export type LearningLogEntryDetail = SprintJournalEntryDetail;

// Mirrors GET /api/learning-lab/reminder (LearningLabController) — the
// Milestone 17 retention nudge, computed live rather than emailed by a
// background job (see LearningLabController's doc comment for why).
export interface LearningLabReminderStatus {
  lastPublishedAt: string | null;
  daysSinceLastEntry: number | null;
  nudgeActive: boolean;
}
