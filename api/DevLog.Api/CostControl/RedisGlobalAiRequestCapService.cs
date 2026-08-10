using StackExchange.Redis;

namespace DevLog.Api.CostControl;

/// <summary>
/// Redis-backed minute bucket counter used for cross-instance global caps.
/// </summary>
public class RedisGlobalAiRequestCapService : IGlobalAiRequestCapService
{
    private readonly IConnectionMultiplexer _connectionMultiplexer;

    public RedisGlobalAiRequestCapService(IConnectionMultiplexer connectionMultiplexer)
    {
        _connectionMultiplexer = connectionMultiplexer;
    }

    public async Task<bool> TryAcquireAsync(string scope, int maxPerMinute, CancellationToken cancellationToken = default)
    {
        var limit = Math.Max(1, maxPerMinute);
        var now = DateTime.UtcNow;
        var minuteBucket = now.ToString("yyyyMMddHHmm");
        var key = $"ai:global:{scope}:{minuteBucket}";

        var db = _connectionMultiplexer.GetDatabase();
        var count = await db.StringIncrementAsync(key).ConfigureAwait(false);

        if (count == 1)
        {
            var nextMinute = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0, DateTimeKind.Utc)
                .AddMinutes(1)
                .AddSeconds(2);
            var ttl = nextMinute - now;
            if (ttl > TimeSpan.Zero)
            {
                await db.KeyExpireAsync(key, ttl).ConfigureAwait(false);
            }
        }

        return count <= limit;
    }
}
