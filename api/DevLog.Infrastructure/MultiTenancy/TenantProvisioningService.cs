using DevLog.Domain.Entities;
using DevLog.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;

namespace DevLog.Infrastructure.MultiTenancy;

public class TenantProvisioningService
{
    private readonly DevLogDbContext _db;
    private readonly ClerkUserService _clerkUserService;
    private readonly ICurrentTenantAccessor _tenantAccessor;

    public TenantProvisioningService(
        DevLogDbContext db,
        ClerkUserService clerkUserService,
        ICurrentTenantAccessor tenantAccessor)
    {
        _db = db;
        _clerkUserService = clerkUserService;
        _tenantAccessor = tenantAccessor;
    }

    public async Task<Tenant> GetOrCreateTenantAsync(string clerkUserId, string? primaryEmail = null, CancellationToken cancellationToken = default)
    {
        var existing = await _db.Tenants.FirstOrDefaultAsync(t => t.UserId == clerkUserId, cancellationToken);
        if (existing is not null)
        {
            if (string.IsNullOrWhiteSpace(existing.PrimaryEmail))
            {
                var backfillPrimaryEmail = await ResolvePrimaryEmailAsync(clerkUserId, primaryEmail, cancellationToken);
                if (!string.IsNullOrWhiteSpace(backfillPrimaryEmail))
                {
                    existing.PrimaryEmail = backfillPrimaryEmail.Trim();
                    existing.NormalizedPrimaryEmail = NormalizeEmail(backfillPrimaryEmail);
                    await _db.SaveChangesAsync(cancellationToken);
                }
            }
            return existing;
        }

        var resolvedPrimaryEmail = await ResolvePrimaryEmailAsync(clerkUserId, primaryEmail, cancellationToken);
        var tenantId = Guid.NewGuid();

        var placeholderSlug = $"dev-{tenantId:N}"[..12];

        var tenant = new Tenant
        {
            Id = tenantId,
            UserId = clerkUserId,
            PrimaryEmail = string.IsNullOrWhiteSpace(resolvedPrimaryEmail) ? null : resolvedPrimaryEmail.Trim(),
            NormalizedPrimaryEmail = string.IsNullOrWhiteSpace(resolvedPrimaryEmail) ? null : NormalizeEmail(resolvedPrimaryEmail),
            Slug = placeholderSlug,
            PlanTier = "free",
            CreatedAt = DateTime.UtcNow
        };

        var portfolio = new Portfolio
        {
            TenantId = tenantId,
            Headline = "New DevLog member",
            CoreTechnologies = new List<string>(),
            Visibility = PortfolioVisibility.Unlisted,
            Discoverable = false
        };

        _db.Tenants.Add(tenant);
        _db.Portfolios.Add(portfolio);

        if (_tenantAccessor is CurrentTenantAccessor accessor)
        {
            accessor.TenantId = tenantId;
        }

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            _db.Entry(tenant).State = EntityState.Detached;
            _db.Entry(portfolio).State = EntityState.Detached;

            var winner = await _db.Tenants.FirstOrDefaultAsync(t => t.UserId == clerkUserId, cancellationToken);
            if (winner is not null)
            {
                return winner;
            }

            throw;
        }

        return tenant;
    }

    private static string NormalizeEmail(string email) =>
        email.Trim().ToLowerInvariant();

    private async Task<string?> ResolvePrimaryEmailAsync(
        string clerkUserId,
        string? primaryEmail,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(primaryEmail))
        {
            return primaryEmail.Trim();
        }

        var clerkPrimaryEmail = await _clerkUserService.GetPrimaryEmailAddressAsync(clerkUserId, cancellationToken);
        return string.IsNullOrWhiteSpace(clerkPrimaryEmail?.EmailAddress)
            ? null
            : clerkPrimaryEmail.EmailAddress.Trim();
    }
}
