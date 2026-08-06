namespace DevLog.Domain.Entities;

public class Tenant
{
    public Guid Id { get; set; }

    public string UserId { get; set; } = string.Empty;

    public string Slug { get; set; } = string.Empty;
    public string PlanTier { get; set; } = "free";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Portfolio? Portfolio { get; set; }
    public ICollection<Experience> Experiences { get; set; } = new List<Experience>();
    public ICollection<RawEntry> RawEntries { get; set; } = new List<RawEntry>();
    public Subscription? Subscription { get; set; }
}
