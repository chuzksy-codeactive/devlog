using DevLog.Infrastructure.Ai.CostControl;

namespace DevLog.Api.CostControl;

public class PostgresGlobalAiRequestCapService(IAiQuotaStore quotaStore) : IGlobalAiRequestCapService
{
    public async Task<bool> TryAcquireAsync(
        string scope,
        int maxPerMinute,
        CancellationToken cancellationToken = default) =>
        await quotaStore.TryAcquireAsync(
            "requests",
            Math.Max(1, maxPerMinute),
            cancellationToken: cancellationToken) == AiQuotaDecision.Allowed;
}
