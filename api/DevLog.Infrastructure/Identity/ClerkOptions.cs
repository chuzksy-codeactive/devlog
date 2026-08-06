namespace DevLog.Infrastructure.Identity;

public class ClerkOptions
{
    public const string SectionName = "Clerk";

    public string Authority { get; set; } = string.Empty;
    public string AuthorizedParty { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
}
