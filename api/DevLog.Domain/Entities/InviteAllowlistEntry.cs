namespace DevLog.Domain.Entities;

public class InviteAllowlistEntry
{
    public Guid Id { get; set; }

    public string Email { get; set; } = string.Empty;
    public string NormalizedEmail { get; set; } = string.Empty;

    public InviteAllowlistStatus Status { get; set; } = InviteAllowlistStatus.Active;

    public string? InvitedBy { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
