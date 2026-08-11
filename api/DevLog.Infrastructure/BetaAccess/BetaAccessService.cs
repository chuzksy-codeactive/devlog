using System.Security.Claims;
using DevLog.Domain.Entities;
using DevLog.Infrastructure.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DevLog.Infrastructure.BetaAccess;

/// <summary>
/// Checks private-beta access from trusted auth claims and invite allowlist state.
/// </summary>
public class BetaAccessService : IBetaAccessService
{
    private readonly DevLogDbContext _db;
    private readonly ClerkUserService _clerkUserService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IOptions<PrivateBetaOptions> _options;

    public BetaAccessService(
        DevLogDbContext db,
        ClerkUserService clerkUserService,
        IHttpContextAccessor httpContextAccessor,
        IOptions<PrivateBetaOptions> options)
    {
        _db = db;
        _clerkUserService = clerkUserService;
        _httpContextAccessor = httpContextAccessor;
        _options = options;
    }

    /// <summary>
    /// Returns allow/deny status for the current request user.
    /// </summary>
    public async Task<BetaAccessResult> EvaluateCurrentUserAsync(CancellationToken cancellationToken = default)
    {
        var options = _options.Value;
        var user = _httpContextAccessor.HttpContext?.User;

        if (user?.Identity?.IsAuthenticated != true)
        {
            return new BetaAccessResult(BetaAccessOutcome.Unauthenticated);
        }

var email = ResolveEmail(user, options.EmailClaimTypes);

if (!options.Enabled)
{
    return new BetaAccessResult(
        BetaAccessOutcome.BetaDisabled,
        string.IsNullOrWhiteSpace(email) ? null : NormalizeEmail(email));
}

var hasVerifiedEmail = HasVerifiedEmailClaim(user, options.EmailVerifiedClaimTypes);

if (string.IsNullOrWhiteSpace(email) || (options.RequireVerifiedEmail && !hasVerifiedEmail))
{
    var clerkUserId = user.FindFirst("sub")?.Value;
    if (!string.IsNullOrWhiteSpace(clerkUserId))
    {
        var primaryEmail = await _clerkUserService.GetPrimaryEmailAddressAsync(clerkUserId, cancellationToken);
        if (string.IsNullOrWhiteSpace(email))
        {
            email = primaryEmail?.EmailAddress;
        }

        if (options.RequireVerifiedEmail && !hasVerifiedEmail)
        {
            hasVerifiedEmail = primaryEmail?.IsVerified == true;
        }
    }
}

        if (string.IsNullOrWhiteSpace(email))
        {
            return new BetaAccessResult(BetaAccessOutcome.MissingEmailClaim);
        }

        var normalizedEmail = NormalizeEmail(email);

        if (IsAdminEmail(options.AdminEmails, normalizedEmail))
        {
            return new BetaAccessResult(BetaAccessOutcome.Allowed, normalizedEmail);
        }

        if (options.RequireVerifiedEmail && !hasVerifiedEmail)
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

    private static bool IsAdminEmail(IEnumerable<string> adminEmails, string normalizedEmail) =>
        adminEmails.Any(admin => NormalizeEmail(admin) == normalizedEmail);
}
