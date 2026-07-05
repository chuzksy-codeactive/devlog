namespace DevLog.Domain.Entities;

public class Experience
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    public ExperienceType ExperienceType { get; set; }

    // Free text on purpose — never a foreign key to a real employer record.
    // For ExperienceType.Employer this holds the sanitized descriptor
    // ("a Series B fintech, payments team"); for PersonalProject/OpenSource
    // it's just a short label for the project.
    public string CompanyLabel { get; set; } = string.Empty;

    // Modeled as two columns rather than a Postgres daterange for EF Core
    // simplicity in this first pass — same information, easier to map.
    // EndDate null means "present."
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }

    public List<string> Technologies { get; set; } = new();
    public string KeyAchievement { get; set; } = string.Empty;
}
