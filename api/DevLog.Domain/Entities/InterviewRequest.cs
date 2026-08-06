namespace DevLog.Domain.Entities;

public class InterviewRequest
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    public string RecruiterEmail { get; set; } = string.Empty;
    public string? RecruiterName { get; set; }
    public string Note { get; set; } = string.Empty;

    public InterviewRequestStatus Status { get; set; } = InterviewRequestStatus.Pending;

    public string? ContactInstructions { get; set; }

    public bool EmailVerified { get; set; }

    public string? VerificationToken { get; set; }
    public DateTime? VerificationTokenExpiresAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? RespondedAt { get; set; }
}
