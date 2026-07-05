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
// polymorphic (published_entries and experiences as two source types), but
// in the schema as actually built, an Experience's narrative content only
// ever reaches the public/RAG layer by first passing through
// raw_entries -> sanitize -> approve as a RawEntryType.Experience, which
// lands in published_entries just like a sprint entry does (Section 3's
// "applies uniformly" note). So PublishedEntry is the only source type that
// exists in practice right now. Kept as an enum (not hard-coded) so a direct
// Experience-row embedding path can be added later without a schema change,
// if/when structured Experience data needs its own RAG representation.
public enum EmbeddingSourceType
{
    PublishedEntry
}
