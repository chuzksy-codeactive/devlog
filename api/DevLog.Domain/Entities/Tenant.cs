namespace DevLog.Domain.Entities;

// The root multi-tenant entity. Every tenant-scoped table (raw_entries,
// published_entries, experiences, ...) carries a TenantId foreign key back
// to this, and RLS policies filter on it. See architecture doc Section 1.
public class Tenant
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Slug { get; set; } = string.Empty;
    public string PlanTier { get; set; } = "free";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Portfolio? Portfolio { get; set; }
    public ICollection<Experience> Experiences { get; set; } = new List<Experience>();
    public ICollection<RawEntry> RawEntries { get; set; } = new List<RawEntry>();
    public Subscription? Subscription { get; set; }
}
