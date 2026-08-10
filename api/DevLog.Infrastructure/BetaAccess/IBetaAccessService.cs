namespace DevLog.Infrastructure.BetaAccess;

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

public record BetaAccessResult(BetaAccessOutcome Outcome, string? ResolvedEmail = null);

public interface IBetaAccessService
{
    Task<BetaAccessResult> EvaluateCurrentUserAsync(CancellationToken cancellationToken = default);
}
