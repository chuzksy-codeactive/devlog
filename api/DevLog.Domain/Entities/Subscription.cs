namespace DevLog.Domain.Entities;

// Tracks usage/quota only in Phase 1 — no Stripe integration yet per the
// MVP roadmap. SanitizationQuota exists specifically so a per-tenant rate
// cap can be enforced (Section 9's cost-abuse fix), not just a monetization
// gate.
public class Subscription
{
    public Guid TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    public string Plan { get; set; } = "free";
    public string AiModelTier { get; set; } = "dev";
    public int SanitizationQuota { get; set; } = 20;
}
