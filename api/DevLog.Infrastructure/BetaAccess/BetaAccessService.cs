using System.Security.Claims;
using DevLog.Domain.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DevLog.Infrastructure.BetaAccess;

public class BetaAccessService : IBetaAccessService
{
    private readonly DevLogDbContext _db;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IOptions<PrivateBetaOptions> _options;

    public BetaAccessService(
        DevLogDbContext db,
        IHttpContextAccessor httpContextAccessor,
        IOptions<PrivateBetaOptions> options)
    {
        _db = db;
        _httpContextAccessor = httpContextAccessor;
        _options = options;
    }

    public async Task<BetaAccessResult> EvaluateCurrentUserAsync(CancellationToken cancellationToken = default)
    {
        var options = _options.Value;
        var user = _httpContextAccessor.HttpContext?.User;

        if (user?.Identity?.IsAuthenticated != true)
        {
            return new BetaAccessResult(BetaAccessOutcome.Unauthenticated);
        }

        var email = ResolveEmail(user, options.EmailClaimTypes);
        if (string.IsNullOrWhiteSpace(email))
        {
            return new BetaAccessResult(BetaAccessOutcome.MissingEmailClaim);
        }

        var normalizedEmail = NormalizeEmail(email);

        if (!options.Enabled)
        {
            return new BetaAccessResult(BetaAccessOutcome.BetaDisabled, normalizedEmail);
        }

        if (options.RequireVerifiedEmail && !HasVerifiedEmailClaim(user, options.EmailVerifiedClaimTypes))
        {
            return new BetaAccessResult(BetaAccessOutcome.EmailNotVerified, normalizedEmail);
        }

        var invite = await _db.InviteAllowlistEntries
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.NormalizedEmail == normalizedEmail, cancellationToken);

        if (invite is null)
        {
            return new BetaAccessResult(BetaAccessOutcome.NotInvited, normalizedEmail);
        }

        if (invite.Status == InviteAllowlistStatus.Revoked)
        {
            return new BetaAccessResult(BetaAccessOutcome.InviteRevoked, normalizedEmail);
        }

        if (invite.Status == InviteAllowlistStatus.Pending)
        {
            return new BetaAccessResult(BetaAccessOutcome.InvitePending, normalizedEmail);
        }

        if (invite.ExpiresAt is DateTime expiresAt && expiresAt <= DateTime.UtcNow)
        {
            return new BetaAccessResult(BetaAccessOutcome.InviteExpired, normalizedEmail);
        }

        return new BetaAccessResult(BetaAccessOutcome.Allowed, normalizedEmail);
    }

    private static string? ResolveEmail(ClaimsPrincipal user, IEnumerable<string> claimTypes)
    {
        foreach (var claimType in claimTypes)
        {
            var value = user.FindFirst(claimType)?.Value;
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }

    private static bool HasVerifiedEmailClaim(ClaimsPrincipal user, IEnumerable<string> verifiedClaimTypes)
    {
        foreach (var claimType in verifiedClaimTypes)
        {
            var value = user.FindFirst(claimType)?.Value;
            if (string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "1", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string NormalizeEmail(string email) =>
        email.Trim().ToLowerInvariant();
}
