namespace DevLog.Domain.Entities;

// Product Brief gap-closure feature #1 / architecture doc Section 6 — "the
// actual north star action... the single most load-bearing addition."
// Deliberately lightweight for Phase 1: no recruiter account exists yet
// (same "lightweight per-request record" shape as AiQueryLog), so
// RecruiterEmail is how the creator reaches back out on accept, not a
// foreign key into a recruiter identity table. That becomes first-class
// only in Phase 2 billing, per the MVP roadmap.
public class InterviewRequest
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    public string RecruiterEmail { get; set; } = string.Empty;
    public string? RecruiterName { get; set; }
    public string Note { get; set; } = string.Empty;

    public InterviewRequestStatus Status { get; set; } = InterviewRequestStatus.Pending;

    // Set only on Accept — "reveals a scheduling link or contact method of
    // the creator's choosing" (architecture doc Section 6). Never populated
    // any other way, so there is no code path that leaks it before the
    // creator explicitly chooses to.
    public string? ContactInstructions { get; set; }

    // Milestone 10 — recruiter email verification (architecture doc Section
    // 9's Phase 1 security item, deliberately deferred until now). Separate
    // from Status on purpose: EmailVerified is "is this request even real,"
    // Status is "what did the creator decide about it" — two independent
    // dimensions, not one combined state machine. Unverified requests never
    // appear in the creator's inbox (CreatorInterviewRequestsController) and
    // never trigger the creator-notification email (Milestone 6) —
    // verification is what makes a submission actually arrive, not just get
    // stored.
    public bool EmailVerified { get; set; }

    // Stores a SHA-256 hash of the one-time verification token (never the
    // raw token itself). Null once verified (or once expired and abandoned).
    public string? VerificationToken { get; set; }
    public DateTime? VerificationTokenExpiresAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? RespondedAt { get; set; }
}
