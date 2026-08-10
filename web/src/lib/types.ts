export type ExperienceType =
  | "Employer"
  | "PersonalProject"
  | "OpenSource"
  | "Freelance"
  | "Education";

export interface ExperienceEntry {
  id: string;
  experienceType: ExperienceType;
  companyLabel: string;
  title: string;
  startDate: string; // ISO date (yyyy-MM-dd)
  endDate: string | null; // null = present
  technologies: string[];
  keyAchievement: string;
}

export type Discipline =
  | "SoftwareEngineer"
  | "ProductManager"
  | "BusinessAnalyst"
  | "DevOps"
  | "ScrumMaster"
  | "Other";

export interface ProofLink {
  label: string;
  url: string;
}

export interface PortfolioData {
  slug: string;
  displayName: string;
  positioningLine: string;
  currentRoleTitle: string | null;
  coreTechnologies: string[];
  githubUrl: string | null;
  githubVerified: boolean;
  hasCv: boolean;
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

export interface SprintJournalEntrySummary {
  id: string;
  title: string;
  tags: string[];
  publishedAt: string; // ISO datetime
}

export interface SprintJournalEntryDetail extends SprintJournalEntrySummary {
  content: string;
}

export type PortfolioVisibility = "Public" | "Unlisted" | "Paused";

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
  gitHubVerified: boolean;
  cvFileUrl: string | null;
  discipline: Discipline;
  proofLinks: ProofLink[];
  visibility: PortfolioVisibility;
  discoverable: boolean;
  notificationEmail: string | null;
}

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

export interface ResumeImportResponse {
  cvFileUrl: string;
  suggestions: OnboardingSuggestions | null;
  message?: string;
}

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

export type LearningLogEntrySummary = SprintJournalEntrySummary;
export type LearningLogEntryDetail = SprintJournalEntryDetail;

export interface LearningLabReminderStatus {
  lastPublishedAt: string | null;
  daysSinceLastEntry: number | null;
  nudgeActive: boolean;
}

export type BetaAccessOutcome =
  | "Allowed"
  | "BetaDisabled"
  | "Unauthenticated"
  | "MissingEmailClaim"
  | "EmailNotVerified"
  | "NotInvited"
  | "InvitePending"
  | "InviteExpired"
  | "InviteRevoked";

export type WaitlistSubmissionStatus = "Pending" | "Approved" | "Rejected";

export interface BetaAccessStatusResponse {
  privateBetaEnabled: boolean;
  outcome: BetaAccessOutcome;
  canAccessCreatorFeatures: boolean;
  resolvedEmail: string | null;
  waitlistSubmissionStatus: WaitlistSubmissionStatus | null;
}

export interface AdminWaitlistSubmission {
  id: string;
  email: string;
  name: string | null;
  reason: string | null;
  useCase: string | null;
  referrer: string | null;
  status: WaitlistSubmissionStatus;
  createdAt: string;
  reviewedAt: string | null;
}

export interface AdminInviteEntry {
  id: string;
  email: string;
  status: "Active" | "Pending" | "Revoked";
  invitedBy: string | null;
  expiresAt: string | null;
  notes: string | null;
  createdAt: string;
  updatedAt: string;
}

export interface AdminUserActivityRow {
  tenantId: string;
  slug: string;
  displayName: string | null;
  tenantCreatedAt: string;
  activityLevel: "HighlyActive" | "ModeratelyActive" | "LowActivity";
  activityScore: number;
  possibleAbuse: boolean;
  lastPortfolioViewAt: string | null;
  lastActivityAt: string | null;
  experienceCount: number;
  rawEntries30d: number;
  publishedEntries30d: number;
  resumeImports30d: number;
  sanitizations30d: number;
  portfolioViews30d: number;
  aiQueries30d: number;
  interviewRequestsVerified30d: number;
}

export interface AdminUserActivityAnalyticsResponse {
  windowDays: number;
  generatedAt: string;
  tenantCount: number;
  highlyActiveCount: number;
  moderatelyActiveCount: number;
  lowActivityCount: number;
  possibleAbuseCount: number;
  mostActive: AdminUserActivityRow[];
  leastActive: AdminUserActivityRow[];
}
