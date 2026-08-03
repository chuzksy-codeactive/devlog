namespace DevLog.Domain.Entities;

// EXPERIENCES.experience_type — see architecture doc Section 1.
// Not employer-only on purpose: the product must work for someone without
// a job yet (new grad, bootcamp graduate, career switcher).
public enum ExperienceType
{
    Employer,
    PersonalProject,
    OpenSource,
    Freelance,
    Education
}

// PORTFOLIOS.visibility — see architecture doc Section 1's "Visibility enforcement" note.
// Paused is enforced at the RLS layer (denied to the anon/public role entirely);
// Unlisted is still readable via direct slug but excluded from Discover.
public enum PortfolioVisibility
{
    Public,
    Unlisted,
    Paused
}

// RAW_ENTRIES.entry_type — generic across sprint entries, experience drafts
// (including onboarding-imported CV content), and learning log entries. All
// three flow through the same sanitize-and-approve pipeline (Section 3).
public enum RawEntryType
{
    SprintEntry,
    Experience,
    LearningLog
}

public enum RawEntryStatus
{
    Draft,
    Sanitizing,
    PendingApproval,
    Approved,
    Rejected
}

// EMBEDDINGS.source_type — architecture doc Section 2 designed this as
// polymorphic (published_entries and experiences as two source types).
// Milestone 4's comment here assumed Experience content would reach RAG
// indirectly, by first passing through raw_entries -> sanitize -> approve
// as a RawEntryType.Experience — but that assumption never actually held:
// Milestone 11 built ExperienceController as direct CRUD straight onto the
// `experiences` table (CompanyLabel is free text the creator types
// themselves, "never a foreign key to a real employer record," already
// pre-anonymized by design — see Experience.cs — so it never needed the
// sanitize pipeline in the first place). That meant Experience rows had
// *no* path to embeddings at all, not even the indirect one this comment
// described, until Milestone 12's Ask AI bugfix added EmbedExperienceAsync
// as a genuine second source type, embedded directly on
// create/update/delete rather than routed through the sanitize pipeline.
public enum EmbeddingSourceType
{
    PublishedEntry,
    Experience
}

// INTERVIEW_REQUESTS.status — architecture doc Section 6 ("Request
// Interview"). Accepting is the only path that reveals the creator's
// contact info (InterviewRequest.ContactInstructions); declining or
// staying pending reveals nothing — DevLog never brokers contact info
// without that explicit creator-initiated accept step.
public enum InterviewRequestStatus
{
    Pending,
    Accepted,
    Declined
}

// PORTFOLIOS.discipline — multi-role expansion (Product Brief's Phase 2
// gap-closure item, MVP roadmap's "Multi-role support"). Deliberately just
// a label/config driver, not a behavioral fork: nothing in the sanitization
// pipeline, RAG, or RLS reads this — it exists purely so the frontend can
// pick the right copy for a field ("Core technologies" vs. "Core skills/tools")
// via a static lookup (web/src/lib/discipline-labels.ts), same shape as
// ExperienceType's UI label map. Defaults to SoftwareEngineer so every
// existing tenant needs zero migration-time action. String-backed
// (.HasConversion<string>() in DevLogDbContext) — adding a new discipline
// later is a pure code change, no migration, same reasoning as every other
// enum in this schema.
public enum PortfolioDiscipline
{
    SoftwareEngineer,
    ProductManager,
    BusinessAnalyst,
    DevOps,
    ScrumMaster,
    Other
}
