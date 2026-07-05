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

    // The LLM-sanitized draft, stored so the creator can review it across
    // requests before approving — set when the sanitize pipeline runs,
    // cleared/overwritten on re-sanitization. Not the same as
    // PublishedEntry.SanitizedContent, which only exists after approval.
    public string? SanitizedPreview { get; set; }
    public string? SanitizationModelVersion { get; set; }

    public PublishedEntry? PublishedEntry { get; set; }
}
