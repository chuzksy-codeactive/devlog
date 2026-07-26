using DevLog.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DevLog.Infrastructure.MultiTenancy;

// Closes the gap that made every authenticated write endpoint in this
// codebase (EntriesController, CreatorInterviewRequestsController,
// PortfolioController) effectively unusable for a real Clerk-issued token:
// nothing ever created a Tenant row, so TenantResolutionMiddleware's old
// tenant_id-claim lookup always came back empty. This service is called
// from that middleware on every authenticated request, keyed on Clerk's
// standard "sub" claim (always present, unlike a custom claim Clerk was
// never configured to issue) rather than a webhook — matches this
// project's running preference for the simpler synchronous option over
// extra infra (same reasoning as "no Hangfire yet" elsewhere).
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
        // No RLS on tenants (see sql/004's comment) — this lookup runs
        // before we know the caller's tenant_id at all, so it can't depend
        // on app.current_tenant being set yet. Nothing sensitive lives on
        // this row (see Tenant.cs), so that's an acceptable trade.
        var existing = await _db.Tenants.FirstOrDefaultAsync(t => t.UserId == clerkUserId, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var tenantId = Guid.NewGuid();

        // Deterministic from the fresh GUID, not a random-and-check loop —
        // guaranteed unique without a lookup, since it's 1:1 with a value
        // that's already guaranteed unique. Genuinely a placeholder: there's
        // no rename endpoint yet (see PortfolioController — Slug isn't part
        // of UpdatePortfolioRequest), so a creator is stuck with this until
        // that's built. Flagged, not fixed, same as the rest of this
        // milestone's known follow-ups.
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

        // portfolios carries RLS (sql/004) — its tenant_isolation policy has
        // no explicit WITH CHECK, so Postgres falls back to the USING
        // expression (tenant_id = current_setting('app.current_tenant')) for
        // the INSERT too. TenantConnectionInterceptor only sets that GUC
        // when ICurrentTenantAccessor.TenantId is already non-null — but
        // nothing sets it until TenantResolutionMiddleware reads this
        // method's return value, which is after this SaveChangesAsync call.
        // Without setting it here first, the connection this INSERT runs on
        // has no app.current_tenant at all, and the brand-new tenantId can
        // never satisfy "tenant_id = NULL" — RLS rejects its own portfolio
        // row. tenants itself has no RLS (see below), so it doesn't need
        // this, but portfolios does. TenantResolutionMiddleware overwrites
        // this with the same (or, on the race path below, the winning)
        // tenant id immediately after this method returns, so setting it
        // early here is only ever a preview of what it's about to become.
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
            // Two near-simultaneous first requests from the same brand-new
            // user both reached here before either committed — the unique
            // index on Tenants.UserId (DevLogDbContext) is what actually
            // prevents a duplicate row; this just means "someone else won,
            // use their row" instead of surfacing a 500 for what's really a
            // benign race.
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
