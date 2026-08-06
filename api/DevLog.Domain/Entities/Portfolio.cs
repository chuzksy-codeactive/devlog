namespace DevLog.Domain.Entities;

public class Portfolio
{
    public Guid TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    public string Headline { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;
    public string PositioningLine { get; set; } = string.Empty;
    public string? PhotoUrl { get; set; }

    public string? CurrentRole { get; set; }
    public string? YearsExperience { get; set; }

    public List<string> CoreTechnologies { get; set; } = new();
    public string? LinkedInUrl { get; set; }
    public string? GitHubUrl { get; set; }

    public bool GitHubVerified { get; set; } = false;

    public string? CvFileUrl { get; set; }

    public PortfolioDiscipline Discipline { get; set; } = PortfolioDiscipline.SoftwareEngineer;

    public List<ProofLink> ProofLinks { get; set; } = new();

    public PortfolioVisibility Visibility { get; set; } = PortfolioVisibility.Unlisted;
    public bool Discoverable { get; set; } = false;

    public string? NotificationEmail { get; set; }
}
