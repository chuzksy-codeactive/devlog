namespace DevLog.Domain.Entities;

public class RawEntry
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    public RawEntryType EntryType { get; set; }

    public string Title { get; set; } = string.Empty;

    public string RawContent { get; set; } = string.Empty;
    public RawEntryStatus Status { get; set; } = RawEntryStatus.Draft;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public string? SanitizedPreview { get; set; }
    public string? SanitizationModelVersion { get; set; }

    public PublishedEntry? PublishedEntry { get; set; }
}
