namespace DevLog.Infrastructure.BetaAccess;

/// <summary>
/// Normalized outcome codes for private-beta access evaluation.
/// </summary>
public enum BetaAccessOutcome
{
    Allowed,
    BetaDisabled,
    Unauthenticated,
    MissingEmailClaim,
    EmailNotVerified,
    NotInvited,
    InvitePending,
    InviteExpired,
    InviteRevoked
}

/// <summary>
/// Result payload from private-beta access checks.
/// </summary>
public record BetaAccessResult(BetaAccessOutcome Outcome, string? ResolvedEmail = null);

/// <summary>
/// Resolves whether the current authenticated user can access private-beta protected features.
/// </summary>
public interface IBetaAccessService
{
    /// <summary>
    /// Evaluates the current request user against private-beta rules.
    /// </summary>
    Task<BetaAccessResult> EvaluateCurrentUserAsync(CancellationToken cancellationToken = default);
}
