namespace DevLog.Domain.Entities;

// The only entry-content table exposed to the public read API and to the
// embedding pipeline (Phase 2+). Physically separate from RawEntry on
// purpose — see architecture doc Section 1.
public class PublishedEntry
{
    public Guid Id { get; set; }
    public Guid RawEntryId { get; set; }
    public RawEntry? RawEntry { get; set; }
    public Guid TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    public string Title { get; set; } = string.Empty;
    public string SanitizedContent { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new();
    public DateTime PublishedAt { get; set; } = DateTime.UtcNow;

    // Denormalized copy of RawEntry.EntryType, not a join — raw_entries has
    // zero RLS grant for the anon/public role (sql/001, deliberately), so
    // SprintJournalController's anonymous reads can never join through to
    // RawEntry to filter by type. Copied once at publish time in
    // SanitizationPipelineService.ApproveAsync instead.
    public RawEntryType SourceEntryType { get; set; }

    public SanitizationAudit? SanitizationAudit { get; set; }
}
