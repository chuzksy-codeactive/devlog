using DevLog.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DevLog.Infrastructure.MultiTenancy;

public class TenantProvisioningService
{
    private readonly DevLogDbContext _db;
    private readonly ICurrentTenantAccessor _tenantAccessor;

    public TenantProvisioningService(DevLogDbContext db, ICurrentTenantAccessor tenantAccessor)
    {
        _db = db;
        _tenantAccessor = tenantAccessor;
    }

    public async Task<Tenant> GetOrCreateTenantAsync(string clerkUserId, CancellationToken cancellationToken = default)
    {
        var existing = await _db.Tenants.FirstOrDefaultAsync(t => t.UserId == clerkUserId, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var tenantId = Guid.NewGuid();

        var placeholderSlug = $"dev-{tenantId:N}"[..12];

        var tenant = new Tenant
        {
            Id = tenantId,
            UserId = clerkUserId,
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
}
