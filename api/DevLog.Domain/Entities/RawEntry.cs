namespace DevLog.Domain.Entities;

// Never exposed publicly — enforced by RLS granting the anon/public role
// zero access to this table (architecture doc Section 1's "critical
// isolation detail"). Every sprint entry, experience draft, and learning
// log entry passes through here before sanitization.
public class RawEntry
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    public RawEntryType EntryType { get; set; }
    public string RawContent { get; set; } = string.Empty;
    public RawEntryStatus Status { get; set; } = RawEntryStatus.Draft;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public PublishedEntry? PublishedEntry { get; set; }
}
