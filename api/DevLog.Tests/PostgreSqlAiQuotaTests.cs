using System.Transactions;
using DevLog.Infrastructure.Ai.CostControl;
using Npgsql;
using Xunit;

namespace DevLog.Tests;

public class PostgreSqlAiQuotaTests
{
    [PostgresQuotaFact]
    public async Task ConcurrentReservations_NeverExceedDailyAllowance()
    {
        await using var database = await QuotaDatabase.CreateAsync();
        var first = database.CreateStore();
        var second = database.CreateStore();

        var decisions = await Task.WhenAll(Enumerable.Range(0, 80).Select(i =>
            (i % 2 == 0 ? first : second).TryAcquireAsync("provider_calls", 1000, 12, 1000)));

        Assert.Equal(12, decisions.Count(result => result == AiQuotaDecision.Allowed));
        Assert.Equal(68, decisions.Count(result => result == AiQuotaDecision.DailyLimit));
        Assert.Equal(12, await database.CountAsync("day_count"));
        Assert.Equal(12, await database.CountAsync("month_count"));
    }

    [PostgresQuotaFact]
    public async Task Reservations_SurviveNewDataSourceAndSchemaReapplication()
    {
        await using var database = await QuotaDatabase.CreateAsync();
        Assert.Equal(AiQuotaDecision.Allowed,
            await database.CreateStore().TryAcquireAsync("provider_calls", 100, 1, 10));
        await database.ApplySchemaAsync();

        await using var newDataSource = NpgsqlDataSource.Create(database.RuntimeConnectionString);
        var restarted = new PostgresAiQuotaStore(newDataSource);
        Assert.Equal(AiQuotaDecision.DailyLimit,
            await restarted.TryAcquireAsync("provider_calls", 100, 1, 10));
    }

    [PostgresQuotaFact]
    public async Task MinuteDenial_DoesNotConsumeDailyOrMonthlyAllowance()
    {
        await using var database = await QuotaDatabase.CreateAsync();
        var store = database.CreateStore();
        Assert.Equal(AiQuotaDecision.Allowed, await store.TryAcquireAsync("provider_calls", 1, 10, 10));
        Assert.Equal(AiQuotaDecision.MinuteLimit, await store.TryAcquireAsync("provider_calls", 1, 10, 10));
        Assert.Equal(1, await database.CountAsync("day_count"));
        Assert.Equal(1, await database.CountAsync("month_count"));

        await database.OwnerSqlAsync(
            "UPDATE devlog_cost_control.ai_quota_counters SET minute_start = minute_start - interval '1 hour'");
        Assert.Equal(AiQuotaDecision.Allowed, await store.TryAcquireAsync("provider_calls", 1, 10, 10));
        Assert.Equal(2, await database.CountAsync("month_count"));
    }

    [PostgresQuotaFact]
    public async Task UtcWindowRollover_ResetsOnlyExpiredWindows()
    {
        await using var database = await QuotaDatabase.CreateAsync();
        var store = database.CreateStore();
        Assert.Equal(AiQuotaDecision.Allowed, await store.TryAcquireAsync("provider_calls", 100, 1, 2));
        Assert.Equal(AiQuotaDecision.DailyLimit, await store.TryAcquireAsync("provider_calls", 100, 1, 2));

        await database.OwnerSqlAsync(
            "UPDATE devlog_cost_control.ai_quota_counters SET day_start = day_start - interval '1 day'");
        Assert.Equal(AiQuotaDecision.Allowed, await store.TryAcquireAsync("provider_calls", 100, 1, 2));
        Assert.Equal(2, await database.CountAsync("month_count"));

        await database.OwnerSqlAsync(
            "UPDATE devlog_cost_control.ai_quota_counters SET day_start = day_start - interval '1 day'");
        Assert.Equal(AiQuotaDecision.MonthlyLimit, await store.TryAcquireAsync("provider_calls", 100, 1, 2));
        await database.OwnerSqlAsync(
            "UPDATE devlog_cost_control.ai_quota_counters SET month_start = month_start - interval '1 month'");
        Assert.Equal(AiQuotaDecision.Allowed, await store.TryAcquireAsync("provider_calls", 100, 1, 2));
        Assert.Equal(1, await database.CountAsync("month_count"));
    }

    [PostgresQuotaFact]
    public async Task ApplicationRollback_DoesNotRefundReservedProviderCall()
    {
        await using var database = await QuotaDatabase.CreateAsync();
        using (new TransactionScope(TransactionScopeAsyncFlowOption.Enabled))
        {
            Assert.Equal(AiQuotaDecision.Allowed,
                await database.CreateStore().TryAcquireAsync("provider_calls", 100, 1, 10));
        }

        Assert.Equal(AiQuotaDecision.DailyLimit,
            await database.CreateStore().TryAcquireAsync("provider_calls", 100, 1, 10));
    }

    [PostgresQuotaFact]
    public async Task RuntimeRole_CanReserveButCannotReadOrChangeCounters()
    {
        await using var database = await QuotaDatabase.CreateAsync();
        Assert.Equal(AiQuotaDecision.Allowed,
            await database.CreateStore().TryAcquireAsync("provider_calls", 100, 10, 10));
        await using var connection = new NpgsqlConnection(database.RuntimeConnectionString);
        await connection.OpenAsync();

        foreach (var sql in new[]
        {
            "SELECT * FROM devlog_cost_control.ai_quota_counters",
            "UPDATE devlog_cost_control.ai_quota_counters SET month_count = 0",
            "DELETE FROM devlog_cost_control.ai_quota_counters",
            "CREATE TABLE devlog_cost_control.unauthorized (id int)"
        })
        {
            await using var command = new NpgsqlCommand(sql, connection);
            var exception = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
            Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, exception.SqlState);
        }
    }

    [PostgresQuotaFact]
    public async Task MissingQuotaFunction_FailsClosed()
    {
        await using var database = await QuotaDatabase.CreateAsync();
        await database.OwnerSqlAsync(
            "DROP FUNCTION devlog_cost_control.try_acquire(text, integer, integer, integer)");

        await Assert.ThrowsAsync<AiQuotaUnavailableException>(() =>
            database.CreateStore().TryAcquireAsync("provider_calls", 100, 10, 10));
    }

    [PostgresQuotaFact]
    public async Task RequestAndProviderCounters_AreIndependent()
    {
        await using var database = await QuotaDatabase.CreateAsync();
        var store = database.CreateStore();
        Assert.Equal(AiQuotaDecision.Allowed, await store.TryAcquireAsync("requests", 1));
        Assert.Equal(AiQuotaDecision.MinuteLimit, await store.TryAcquireAsync("requests", 1));
        Assert.Equal(AiQuotaDecision.Allowed, await store.TryAcquireAsync("provider_calls", 100, 1, 10));
        Assert.Equal(1, await database.CountAsync("month_count"));
    }

    [PostgresQuotaFact]
    public async Task SqlFunction_RejectsUnexpectedScopeAndMissingProviderAllowances()
    {
        await using var database = await QuotaDatabase.CreateAsync();
        var store = database.CreateStore();
        foreach (var operation in new Func<Task<AiQuotaDecision>>[]
        {
            () => store.TryAcquireAsync("tenant_supplied_scope", 100, 10, 10),
            () => store.TryAcquireAsync("provider_calls", 100),
            () => store.TryAcquireAsync("provider_calls", 100, 10)
        })
        {
            var error = await Assert.ThrowsAsync<AiQuotaUnavailableException>(operation);
            Assert.Equal(PostgresErrorCodes.InvalidParameterValue,
                Assert.IsType<PostgresException>(error.InnerException).SqlState);
        }
        Assert.Equal(AiQuotaDecision.Allowed, await store.TryAcquireAsync("provider_calls", 100, 1, 1));
    }

    private sealed class QuotaDatabase : IAsyncDisposable
    {
        private readonly string _databaseName = $"devlog_quota_test_{Guid.NewGuid():N}";
        private readonly string _roleName = $"quota_app_{Guid.NewGuid():N}";
        private readonly string _password = Guid.NewGuid().ToString("N");
        private readonly NpgsqlConnectionStringBuilder _admin;
        private NpgsqlDataSource? _runtime;
        private bool _databaseCreated;
        private bool _roleCreated;

        private QuotaDatabase(string connectionString)
        {
            _admin = new NpgsqlConnectionStringBuilder(connectionString);
            if (_admin.Host is not ("localhost" or "127.0.0.1" or "::1"))
            {
                throw new InvalidOperationException("Quota integration tests only accept a disposable localhost PostgreSQL server.");
            }
            _admin.Pooling = false;
        }

        public string RuntimeConnectionString => new NpgsqlConnectionStringBuilder(_admin.ConnectionString)
        {
            Database = _databaseName,
            Username = _roleName,
            Password = _password,
            Pooling = true,
            MaxPoolSize = 20
        }.ConnectionString;

        public static async Task<QuotaDatabase> CreateAsync()
        {
            var database = new QuotaDatabase(Environment.GetEnvironmentVariable("DEVLOG_QUOTA_TEST_CONNECTION")
                ?? throw new InvalidOperationException("DEVLOG_QUOTA_TEST_CONNECTION is required."));
            var initialized = false;
            try
            {
                await database.AdminSqlAsync($"CREATE DATABASE \"{database._databaseName}\"");
                database._databaseCreated = true;
                await database.AdminSqlAsync($"CREATE ROLE \"{database._roleName}\" LOGIN PASSWORD '{database._password}'");
                database._roleCreated = true;
                await database.ApplySchemaAsync();
                database._runtime = NpgsqlDataSource.Create(database.RuntimeConnectionString);
                initialized = true;
                return database;
            }
            finally
            {
                if (!initialized)
                {
                    await database.DisposeAsync();
                }
            }
        }

        public PostgresAiQuotaStore CreateStore() =>
            new(_runtime ?? throw new InvalidOperationException("Test database has not been initialized."));

        public Task ApplySchemaAsync() => OwnerSqlAsync(
            File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "sql", "010_ai_cost_control.sql"))
                .Replace("devlog_app", _roleName, StringComparison.Ordinal));

        public async Task<int> CountAsync(string column)
        {
            Assert.Contains(column, new[] { "day_count", "month_count" });
            await using var connection = new NpgsqlConnection(OwnerConnectionString());
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand(
                $"SELECT {column} FROM devlog_cost_control.ai_quota_counters WHERE scope = 'provider_calls'", connection);
            return (int)(await command.ExecuteScalarAsync()
                ?? throw new InvalidOperationException("Expected quota counter was missing."));
        }

        public async Task OwnerSqlAsync(string sql)
        {
            await using var connection = new NpgsqlConnection(OwnerConnectionString());
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand(sql, connection);
            await command.ExecuteNonQueryAsync();
        }

        private string OwnerConnectionString() =>
            new NpgsqlConnectionStringBuilder(_admin.ConnectionString) { Database = _databaseName }.ConnectionString;

        private async Task AdminSqlAsync(string sql)
        {
            await using var connection = new NpgsqlConnection(_admin.ConnectionString);
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand(sql, connection);
            await command.ExecuteNonQueryAsync();
        }

        public async ValueTask DisposeAsync()
        {
            if (_runtime is not null)
            {
                await _runtime.DisposeAsync();
            }
            if (_databaseCreated)
            {
                await AdminSqlAsync($"DROP DATABASE \"{_databaseName}\" WITH (FORCE)");
            }
            if (_roleCreated)
            {
                await AdminSqlAsync($"DROP ROLE \"{_roleName}\"");
            }
        }
    }
}

public sealed class PostgresQuotaFactAttribute : FactAttribute
{
    public PostgresQuotaFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DEVLOG_QUOTA_TEST_CONNECTION")))
        {
            Skip = "Requires DEVLOG_QUOTA_TEST_CONNECTION pointing to a disposable localhost PostgreSQL 18 server.";
        }
    }
}
