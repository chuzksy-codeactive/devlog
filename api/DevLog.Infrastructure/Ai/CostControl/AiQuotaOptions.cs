namespace DevLog.Infrastructure.Ai.CostControl;

public class AiQuotaOptions
{
    public const string SectionName = "AiQuotas";

    public bool Enabled { get; set; }
    public int MaxProviderCallsPerMinute { get; set; } = 20;
    public int MaxProviderCallsPerDay { get; set; } = 100;
    public int MaxProviderCallsPerMonth { get; set; } = 1000;
}
