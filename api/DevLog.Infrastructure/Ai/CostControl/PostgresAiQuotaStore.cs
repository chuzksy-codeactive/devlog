using Npgsql;
using NpgsqlTypes;
using System.Transactions;

namespace DevLog.Infrastructure.Ai.CostControl;

public class PostgresAiQuotaStore(NpgsqlDataSource dataSource) : IAiQuotaStore
{
    public async Task<AiQuotaDecision> TryAcquireAsync(
        string scope,
        int minuteLimit,
        int? dailyLimit = null,
        int? monthlyLimit = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(minuteLimit, 1);
        if (dailyLimit is < 1 || monthlyLimit is < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(dailyLimit), "AI allowances must be positive.");
        }

        try
        {
            // Separate autocommit connection: a later application rollback must not refund a provider call.
            using var suppression = new TransactionScope(
                TransactionScopeOption.Suppress, TransactionScopeAsyncFlowOption.Enabled);
            await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText =
                "SELECT devlog_cost_control.try_acquire(@scope, @minute_limit, @daily_limit, @monthly_limit)";
            command.CommandTimeout = 10;
            command.Parameters.AddWithValue("scope", NpgsqlDbType.Text, scope);
            command.Parameters.AddWithValue("minute_limit", NpgsqlDbType.Integer, minuteLimit);
            command.Parameters.AddWithValue("daily_limit", NpgsqlDbType.Integer, (object?)dailyLimit ?? DBNull.Value);
            command.Parameters.AddWithValue("monthly_limit", NpgsqlDbType.Integer, (object?)monthlyLimit ?? DBNull.Value);

            return await command.ExecuteScalarAsync(cancellationToken) switch
            {
                "allowed" => AiQuotaDecision.Allowed,
                "minute" => AiQuotaDecision.MinuteLimit,
                "day" => AiQuotaDecision.DailyLimit,
                "month" => AiQuotaDecision.MonthlyLimit,
                _ => throw new AiQuotaUnavailableException("The AI quota store returned an invalid decision.")
            };
        }
        catch (NpgsqlException ex)
        {
            throw new AiQuotaUnavailableException("The AI quota store could not be reached.", ex);
        }
        catch (TimeoutException ex)
        {
            throw new AiQuotaUnavailableException("The AI quota store timed out.", ex);
        }
    }
}
