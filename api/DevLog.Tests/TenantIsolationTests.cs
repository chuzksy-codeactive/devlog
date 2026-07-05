using DevLog.Domain.Entities;
using DevLog.Infrastructure;
using DevLog.Infrastructure.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DevLog.Tests;

// This is the Sprint 0 exit-criterion test from the checklist — do not skip
// it. It requires the docker-compose Postgres running AND
// sql/001_row_level_security.sql already applied. It proves RLS itself
// blocks cross-tenant reads, not application code: the query below
// deliberately has no WHERE tenant_id clause.
public class TenantIsolationTests
{
    private const string ConnectionString =
        "Host=localhost;Port=5432;Database=devlog;Username=devlog;Password=devlog_local";

    private static DevLogDbContext CreateContext(Guid? tenantId)
    {
        var tenantAccessor = new CurrentTenantAccessor { TenantId = tenantId };
        var interceptor = new TenantConnectionInterceptor(tenantAccessor);

        var options = new DbContextOptionsBuilder<DevLogDbContext>()
            .UseNpgsql(ConnectionString)
            .AddInterceptors(interceptor)
            .Options;

        return new DevLogDbContext(options);
    }

    [Fact]
    public async Task TenantA_Cannot_See_TenantB_RawEntries()
    {
        var tenantAId = Guid.NewGuid();
        var tenantBId = Guid.NewGuid();

        await using (var seedA = CreateContext(tenantAId))
        {
            seedA.Tenants.Add(new Tenant { Id = tenantAId, UserId = Guid.NewGuid(), Slug = $"tenant-a-{tenantAId}" });
            seedA.RawEntries.Add(new RawEntry
            {
                Id = Guid.NewGuid(),
                TenantId = tenantAId,
                EntryType = RawEntryType.SprintEntry,
                RawContent = "Tenant A's private draft"
            });
            await seedA.SaveChangesAsync();
        }

        await using (var seedB = CreateContext(tenantBId))
        {
            seedB.Tenants.Add(new Tenant { Id = tenantBId, UserId = Guid.NewGuid(), Slug = $"tenant-b-{tenantBId}" });
            seedB.RawEntries.Add(new RawEntry
            {
                Id = Guid.NewGuid(),
                TenantId = tenantBId,
                EntryType = RawEntryType.SprintEntry,
                RawContent = "Tenant B's private draft"
            });
            await seedB.SaveChangesAsync();
        }

        await using var queryAsTenantA = CreateContext(tenantAId);

        // Deliberately no .Where(x => x.TenantId == tenantAId) here.
        var visibleToTenantA = await queryAsTenantA.RawEntries.ToListAsync();

        Assert.Single(visibleToTenantA);
        Assert.Equal(tenantAId, visibleToTenantA[0].TenantId);
        Assert.DoesNotContain(visibleToTenantA, e => e.TenantId == tenantBId);
    }

    [Fact]
    public async Task NoTenantContext_Sees_Nothing()
    {
        // A connection with app.current_tenant never set (e.g. a background
        // job that forgot to set tenant context) should see zero rows, not
        // everything. This is what current_setting(..., true) resolving to
        // NULL should produce against the RLS policy.
        await using var seed = CreateContext(Guid.NewGuid());
        var tenantId = Guid.NewGuid();
        seed.Tenants.Add(new Tenant { Id = tenantId, UserId = Guid.NewGuid(), Slug = $"tenant-{tenantId}" });
        seed.RawEntries.Add(new RawEntry
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            EntryType = RawEntryType.SprintEntry,
            RawContent = "Some draft"
        });
        await seed.SaveChangesAsync();

        await using var queryWithNoTenant = CreateContext(tenantId: null);
        var visible = await queryWithNoTenant.RawEntries.ToListAsync();

        Assert.Empty(visible);
    }
}
