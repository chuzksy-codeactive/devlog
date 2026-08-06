namespace DevLog.Domain.Entities;

public class Experience
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    public ExperienceType ExperienceType { get; set; }

    public string CompanyLabel { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }

    public List<string> Technologies { get; set; } = new();
    public string KeyAchievement { get; set; } = string.Empty;
}
