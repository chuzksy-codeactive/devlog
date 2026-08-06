namespace DevLog.Domain.Entities;

public class Subscription
{
    public Guid TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    public string Plan { get; set; } = "free";
    public string AiModelTier { get; set; } = "dev";
    public int SanitizationQuota { get; set; } = 20;
}
