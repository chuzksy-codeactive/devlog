namespace DevLog.Domain.Entities;

public class Portfolio
{
    public Guid TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    public string Headline { get; set; } = string.Empty;

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
}
