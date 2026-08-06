using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;

namespace DevLog.Infrastructure.MultiTenancy;

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
