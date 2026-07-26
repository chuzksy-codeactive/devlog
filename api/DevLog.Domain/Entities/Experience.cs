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

    // Milestone 11 addition — role/position title ("Backend Engineer"),
    // distinct from CompanyLabel. Like CompanyLabel this was already
    // expected by the frontend's ExperienceEntry type since Milestone 3
    // (mock data always had both), but the entity never actually carried
    // it — a real gap, not a deferral.
    public string Title { get; set; } = string.Empty;

    // Modeled as two columns rather than a Postgres daterange for EF Core
    // simplicity in this first pass — same information, easier to map.
    // EndDate null means "present."
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }

    public List<string> Technologies { get; set; } = new();
    public string KeyAchievement { get; set; } = string.Empty;
}
