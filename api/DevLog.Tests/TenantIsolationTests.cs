using DevLog.Domain.Entities;
using DevLog.Infrastructure;
using DevLog.Infrastructure.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace DevLog.Tests;

public class TenantIsolationTests
{
    private const string ConnectionString =
        "Host=localhost;Port=5432;Database=devlog;Username=devlog_app;Password=devlog_app_local";
    private static readonly NpgsqlDataSource DataSource = BuildDataSource();

    private static NpgsqlDataSource BuildDataSource()
    {
        var dataSourceBuilder = new NpgsqlDataSourceBuilder(ConnectionString);
        dataSourceBuilder.UseVector();
        return dataSourceBuilder.Build();
    }

    private static DevLogDbContext CreateContext(Guid? tenantId)
    {
        var tenantAccessor = new CurrentTenantAccessor { TenantId = tenantId };
        var interceptor = new TenantConnectionInterceptor(tenantAccessor);

        var options = new DbContextOptionsBuilder<DevLogDbContext>()
            .UseNpgsql(DataSource, npgsqlOptions => npgsqlOptions.UseVector())
            .UseSnakeCaseNamingConvention()
            .AddInterceptors(interceptor)
            .Options;

        return new DevLogDbContext(options);
    }

    private static async Task<bool> IsDatabaseAvailableAsync()
    {
        try
        {
            await using var connection = await DataSource.OpenConnectionAsync();
            await using var command = new NpgsqlCommand("SELECT 1", connection);
            await command.ExecuteScalarAsync();
            return true;
        }
        catch
        {
            return false;
        }
    }

    [Fact]
    public async Task TenantA_Cannot_See_TenantB_RawEntries()
    {
        if (!await IsDatabaseAvailableAsync())
        {
            return;
        }

        var tenantAId = Guid.NewGuid();
        var tenantBId = Guid.NewGuid();
        try
        {
            await using (var seedA = CreateContext(tenantAId))
            {
                seedA.Tenants.Add(new Tenant { Id = tenantAId, UserId = $"user_{Guid.NewGuid()}", Slug = $"tenant-a-{tenantAId}" });
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
                seedB.Tenants.Add(new Tenant { Id = tenantBId, UserId = $"user_{Guid.NewGuid()}", Slug = $"tenant-b-{tenantBId}" });
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

            var visibleToTenantA = await queryAsTenantA.RawEntries.ToListAsync();

            Assert.Single(visibleToTenantA);
            Assert.Equal(tenantAId, visibleToTenantA[0].TenantId);
            Assert.DoesNotContain(visibleToTenantA, e => e.TenantId == tenantBId);
        }
        finally
        {
            await DeleteTenantDataAsync(tenantAId);
            await DeleteTenantDataAsync(tenantBId);
        }
    }

    [Fact]
    public async Task NoTenantContext_Sees_Nothing()
    {
        if (!await IsDatabaseAvailableAsync())
        {
            return;
        }

        var tenantId = Guid.NewGuid();
        try
        {
            await using var seed = CreateContext(tenantId);
            seed.Tenants.Add(new Tenant { Id = tenantId, UserId = $"user_{Guid.NewGuid()}", Slug = $"tenant-{tenantId}" });
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
        finally
        {
            await DeleteTenantDataAsync(tenantId);
        }
    }

    private static async Task DeleteTenantDataAsync(Guid tenantId)
    {
        await using var cleanup = CreateContext(tenantId);
        var rawEntries = await cleanup.RawEntries.Where(x => x.TenantId == tenantId).ToListAsync();
        if (rawEntries.Count > 0)
        {
            cleanup.RawEntries.RemoveRange(rawEntries);
        }

        var tenant = await cleanup.Tenants.FirstOrDefaultAsync(x => x.Id == tenantId);
        if (tenant is not null)
        {
            cleanup.Tenants.Remove(tenant);
        }

        await cleanup.SaveChangesAsync();
    }
}
