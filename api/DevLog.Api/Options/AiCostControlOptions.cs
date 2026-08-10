namespace DevLog.Api.Options;

public class AiCostControlOptions
{
    public const string SectionName = "AiCostControl";

    public bool Enabled { get; set; } = true;
    public bool KillSwitchEnabled { get; set; } = false;

    public int GlobalExpensiveRequestsPerMinute { get; set; } = 120;

    public int AskAiPerMinutePerUser { get; set; } = 10;
    public int AskAiPerMinutePerIp { get; set; } = 5;

    public int ResumeImportPerHourPerUser { get; set; } = 5;
    public int ResumeImportPerHourPerIp { get; set; } = 3;

    public int SanitizePerHourPerUser { get; set; } = 10;
    public int SanitizePerHourPerIp { get; set; } = 5;
}
