namespace DevLog.Infrastructure.Ai.CostControl;

public enum AiQuotaDecision
{
    Allowed,
    MinuteLimit,
    DailyLimit,
    MonthlyLimit
}

public interface IAiQuotaStore
{
    Task<AiQuotaDecision> TryAcquireAsync(
        string scope,
        int minuteLimit,
        int? dailyLimit = null,
        int? monthlyLimit = null,
        CancellationToken cancellationToken = default);
}

public class AiQuotaRejectedException(string reason) : Exception("AI allowance is exhausted.")
{
    public string Reason { get; } = reason;
}

public class AiQuotaUnavailableException(string message, Exception? innerException = null)
    : Exception(message, innerException);

public class AiQuotaPausedException() : Exception("AI calls are temporarily disabled.");
