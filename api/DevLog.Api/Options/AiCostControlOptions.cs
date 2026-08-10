namespace DevLog.Api.Options;

/// <summary>
/// Central configuration for AI cost and abuse protection limits.
/// </summary>
public class AiCostControlOptions
{
    public const string SectionName = "AiCostControl";

    /// <summary>
    /// Enables or bypasses request-layer AI cost controls.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Emergency switch to temporarily disable expensive AI endpoints.
    /// </summary>
    public bool KillSwitchEnabled { get; set; } = false;

    /// <summary>
    /// Global cap across all expensive AI requests per minute.
    /// </summary>
    public int GlobalExpensiveRequestsPerMinute { get; set; } = 120;

    public int AskAiPerMinutePerUser { get; set; } = 10;
    public int AskAiPerMinutePerIp { get; set; } = 5;

    public int ResumeImportPerHourPerUser { get; set; } = 5;
    public int ResumeImportPerHourPerIp { get; set; } = 3;

    public int SanitizePerHourPerUser { get; set; } = 10;
    public int SanitizePerHourPerIp { get; set; } = 5;
}
