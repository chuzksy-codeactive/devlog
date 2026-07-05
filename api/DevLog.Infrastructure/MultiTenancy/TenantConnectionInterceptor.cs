using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;

namespace DevLog.Infrastructure.MultiTenancy;

// This is the "defense-in-depth" half of tenant isolation described in
// architecture doc Section 1: even if application code forgets a
// WHERE tenant_id = ... clause, Postgres RLS refuses to return another
// tenant's rows, because this interceptor sets app.current_tenant on every
// connection before any query runs on it.
//
// Uses set_config() rather than a raw "SET app.current_tenant = ..."
// string so the tenant id is passed as a parameter, not string-interpolated
// into SQL.
public class TenantConnectionInterceptor : DbConnectionInterceptor
{
    private readonly ICurrentTenantAccessor _tenantAccessor;

    public TenantConnectionInterceptor(ICurrentTenantAccessor tenantAccessor)
    {
        _tenantAccessor = tenantAccessor;
    }

    public override async Task ConnectionOpenedAsync(
        DbConnection connection,
        ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        await base.ConnectionOpenedAsync(connection, eventData, cancellationToken);
        await SetTenantContextAsync(connection, cancellationToken);
    }

    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        base.ConnectionOpened(connection, eventData);
        SetTenantContextAsync(connection, CancellationToken.None).GetAwaiter().GetResult();
    }

    private async Task SetTenantContextAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        if (_tenantAccessor.TenantId is not Guid tenantId || connection is not NpgsqlConnection npgsqlConnection)
        {
            return;
        }

        await using var command = npgsqlConnection.CreateCommand();
        command.CommandText = "SELECT set_config('app.current_tenant', @tenantId, false);";
        command.Parameters.AddWithValue("tenantId", tenantId.ToString());
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
