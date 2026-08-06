namespace DevLog.Domain.Entities;

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

    public RawEntryType SourceEntryType { get; set; }

    public SanitizationAudit? SanitizationAudit { get; set; }
}
