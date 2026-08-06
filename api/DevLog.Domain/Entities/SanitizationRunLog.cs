namespace DevLog.Domain.Entities;

public class SanitizationRunLog
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
