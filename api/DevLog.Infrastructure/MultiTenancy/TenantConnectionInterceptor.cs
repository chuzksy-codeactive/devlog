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
//
// Milestone 12 bugfix: this used to skip the set_config call entirely for
// anonymous requests (TenantId == null), on the assumption that "unset"
// connections just don't have app.current_tenant. That's wrong once you
// account for how NpgsqlDataSource is registered — as a singleton
// (ServiceCollectionExtensions.cs), which pools and reuses PHYSICAL
// Postgres connections across unrelated requests. set_config's third
// argument (is_local=false) makes the setting stick for the rest of that
// Postgres session, not just the current transaction — so a connection an
// authenticated request used (setting app.current_tenant to a real GUID)
// could later be handed to a totally different, anonymous request, which
// would silently inherit the previous caller's tenant context because this
// interceptor never told Postgres to clear it.
//
// Do not send NULL for anonymous requests. Once a custom GUC has existed in
// a Postgres session, clearing it can leave current_setting(..., true)
// observable as an empty string, and RLS policies cast that value to uuid.
// Sending Guid.Empty instead keeps the value syntactically valid while still
// matching no real tenant row.
public class TenantConnectionInterceptor : DbConnectionInterceptor
{
    private static readonly string AnonymousTenantId = Guid.Empty.ToString();

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
        if (connection is not NpgsqlConnection npgsqlConnection)
        {
            return;
        }

        await using var command = npgsqlConnection.CreateCommand();
        command.CommandText = "SELECT set_config('app.current_tenant', @tenantId, false);";
        command.Parameters.AddWithValue("tenantId", _tenantAccessor.TenantId?.ToString() ?? AnonymousTenantId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
