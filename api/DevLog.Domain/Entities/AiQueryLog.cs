namespace DevLog.Domain.Entities;

// Lightweight per-request record for now, not a first-class recruiter
// account — matches the MVP roadmap's Phase 1 note that recruiter accounts
// are "the lightweight per-request record they were in Phase 1" and become
// first-class/tiered only in Phase 2 billing. Exists to (a) give the hourly
// rate cap something to count against (the cost-abuse control Section 9
// flagged — Redis-backed counters are a later refinement of the same idea)
// and (b) leave an audit trail of what recruiters asked, without requiring
// a recruiter to log in to ask a question (roadmap: "anonymous browsing...
// single-candidate Ask AI").
public class AiQueryLog
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    public string Question { get; set; } = string.Empty;

    // A coarse, non-identifying signal for the rate cap (an HMAC-hashed IP),
    // deliberately not a recruiter identity, since none exists yet in Phase 1.
    public string? AskerHash { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
