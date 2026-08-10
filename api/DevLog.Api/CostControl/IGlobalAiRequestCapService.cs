namespace DevLog.Api.CostControl;

/// <summary>
/// Shared counter abstraction for enforcing global AI request caps.
/// </summary>
public interface IGlobalAiRequestCapService
{
    /// <summary>
    /// Attempts to consume one slot from the current minute bucket.
    /// </summary>
    Task<bool> TryAcquireAsync(string scope, int maxPerMinute, CancellationToken cancellationToken = default);
}
