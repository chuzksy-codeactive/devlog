namespace DevLog.Infrastructure.Security;

public class IpHashingOptions
{
    public const string SectionName = "IpHashing";

    public string Secret { get; set; } = string.Empty;
}
