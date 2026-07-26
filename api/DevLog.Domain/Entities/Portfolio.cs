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
    public string? CvFileUrl { get; set; }

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
