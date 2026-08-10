namespace DevLog.Domain.Entities;

public class WaitlistSubmission
{
    public Guid Id { get; set; }

    public string Email { get; set; } = string.Empty;
    public string NormalizedEmail { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string? Reason { get; set; }
    public string? UseCase { get; set; }
    public string? Referrer { get; set; }

    public WaitlistSubmissionStatus Status { get; set; } = WaitlistSubmissionStatus.Pending;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReviewedAt { get; set; }
}
