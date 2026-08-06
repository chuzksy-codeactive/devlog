namespace DevLog.Domain.Entities;

public class AiQueryLog
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    public string Question { get; set; } = string.Empty;

    public string? AskerHash { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
