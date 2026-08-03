namespace DevLog.Domain.Entities;

public class Portfolio
{
    public Guid TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    public string Headline { get; set; } = string.Empty;

    // Milestone 11 addition — the public Home page (Hero.tsx) needed these
    // from Milestone 3 onward but nothing on the backend ever carried them;
    // the page ran entirely on mock data until now. DisplayName is the
    // person's name (never inferred from Tenant/Clerk data — a creator
    // types it deliberately, same "never trust an implicit source for
    // public-facing identity" reasoning as everything else in this schema).
    // PositioningLine is the one-line hero statement ("not a generic 'About
    // me'" per the Product Brief). Both default to empty string, not a
    // placeholder — TenantProvisioningService's auto-provisioned portfolio
    // leaves them blank until the creator fills them in via PUT /me.
    public string DisplayName { get; set; } = string.Empty;
    public string PositioningLine { get; set; } = string.Empty;
    public string? PhotoUrl { get; set; }

    // Nullable/free-text on purpose — "Current role" and "years of experience"
    // need a graceful non-employed state ("Actively building", "Aspiring
    // backend engineer") rather than forcing a job title or a "0 years" stat.
    // See product brief's Home page note.
    public string? CurrentRole { get; set; }
    public string? YearsExperience { get; set; }

    public List<string> CoreTechnologies { get; set; } = new();
    public string? LinkedInUrl { get; set; }
    public string? GitHubUrl { get; set; }

    // Milestone 19 — closes the "anyone could type someone else's GitHub
    // link" gap Chika flagged. True only once ClerkUserService has confirmed
    // a verified GitHub external account on the signed-in Clerk user (see
    // PortfolioController's POST me/github/sync) — never set by a creator
    // typing a URL. While true, GitHubUrl is sourced from that verified
    // account and UpdateMyPortfolio ignores further edits to it; the
    // Milestone 13 GitHub-import endpoint also requires this before it will
    // pull suggestions, using the verified username instead of a
    // client-submitted one. Defaults false so every existing tenant reads
    // as unverified until they explicitly connect.
    public bool GitHubVerified { get; set; } = false;

    public string? CvFileUrl { get; set; }

    // Multi-role expansion (MVP roadmap Phase 2 item, deliberately deferred
    // until the goal was "onboard other developers," then picked back up
    // once dogfooding widened past that). Discipline drives which label
    // variant the frontend renders for a handful of fields (see
    // web/src/lib/discipline-labels.ts) — nothing in this backend branches
    // on it. Defaults to SoftwareEngineer so every tenant created before
    // this field existed reads as unchanged.
    public PortfolioDiscipline Discipline { get; set; } = PortfolioDiscipline.SoftwareEngineer;

    // Generalized alternative/supplement to GitHubUrl above — "GitHub
    // becomes one of several optional proof links (portfolio site,
    // published articles, certifications)" per the Product Brief.
    // Deliberately additive rather than a replacement: GitHubUrl/LinkedInUrl/
    // CvFileUrl stay exactly as they are (common defaults every discipline
    // can use, and removing them would drop already-filled-in creator data
    // for no real benefit) — this is for anything else a discipline needs
    // that doesn't have its own fixed field.
    public List<ProofLink> ProofLinks { get; set; } = new();

    public PortfolioVisibility Visibility { get; set; } = PortfolioVisibility.Unlisted;
    public bool Discoverable { get; set; } = false;

    // Creator-supplied — backs the interview-request notification email
    // (architecture doc Section 6: "on insert, a job sends a notification
    // email to the creator"). Optional: if unset, requests still save
    // successfully, the creator just has to poll GET /api/interview-requests
    // instead of getting notified. No dedicated update endpoint exists yet
    // to set this via the API — same pre-existing gap as Portfolio having
    // no PUT/PATCH endpoint at all (PortfolioController only has GET /me
    // today); set directly via SQL/EF for now.
    public string? NotificationEmail { get; set; }
}
