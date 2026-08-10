using System.Collections.Concurrent;

namespace DevLog.Api.CostControl;

/// <summary>
/// Local fallback used when Redis is not configured.
/// </summary>
public class InMemoryGlobalAiRequestCapService : IGlobalAiRequestCapService
{
    private readonly ConcurrentDictionary<string, int> _counts = new();
    private DateTime _currentMinuteUtc = TruncateToMinute(DateTime.UtcNow);
    private readonly object _gate = new();

    public Task<bool> TryAcquireAsync(string scope, int maxPerMinute, CancellationToken cancellationToken = default)
    {
        var nowMinute = TruncateToMinute(DateTime.UtcNow);
        lock (_gate)
        {
            if (nowMinute != _currentMinuteUtc)
            {
                _counts.Clear();
                _currentMinuteUtc = nowMinute;
            }

            var current = _counts.AddOrUpdate(scope, 1, static (_, existing) => existing + 1);
            return Task.FromResult(current <= Math.Max(1, maxPerMinute));
        }
    }

    private static DateTime TruncateToMinute(DateTime value) =>
        new(value.Year, value.Month, value.Day, value.Hour, value.Minute, 0, DateTimeKind.Utc);
}
