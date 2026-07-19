namespace DevLog.Domain.Entities;

// The root multi-tenant entity. Every tenant-scoped table (raw_entries,
// published_entries, experiences, ...) carries a TenantId foreign key back
// to this, and RLS policies filter on it. See architecture doc Section 1.
public class Tenant
{
    public Guid Id { get; set; }

    // Clerk's user id (the JWT's "sub" claim) — a string like "user_2abc...",
    // never a GUID. This was originally typed as Guid, which meant no real
    // Clerk-issued token could ever be matched to a Tenant row at all; fixed
    // as part of wiring up real auto-provisioning (see
    // TenantProvisioningService). Unique per Clerk user — see
    // DevLogDbContext's index.
    public string UserId { get; set; } = string.Empty;

    public string Slug { get; set; } = string.Empty;
    public string PlanTier { get; set; } = "free";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Portfolio? Portfolio { get; set; }
    public ICollection<Experience> Experiences { get; set; } = new List<Experience>();
    public ICollection<RawEntry> RawEntries { get; set; } = new List<RawEntry>();
    public Subscription? Subscription { get; set; }
}
