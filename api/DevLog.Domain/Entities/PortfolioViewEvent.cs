namespace DevLog.Domain.Entities;

public class PortfolioViewEvent
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    public string? ViewerHash { get; set; }

    public DateTime ViewedAt { get; set; } = DateTime.UtcNow;
}
