namespace DevLog.Domain.Entities;

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
