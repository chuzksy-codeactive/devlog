namespace DevLog.Domain.Entities;

// Written every time a raw entry is sanitized and approved. This is what
// makes the anonymization pipeline defensible after the fact if a dispute
// ever arises — see architecture doc Section 3.
public class SanitizationAudit
{
    public Guid Id { get; set; }
    public Guid PublishedEntryId { get; set; }
    public PublishedEntry? PublishedEntry { get; set; }

    public string ModelVersion { get; set; } = string.Empty;
    public string Diff { get; set; } = string.Empty;
    public string ApprovedBy { get; set; } = string.Empty;
    public DateTime ApprovedAt { get; set; } = DateTime.UtcNow;
}
