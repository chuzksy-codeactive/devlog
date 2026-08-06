namespace DevLog.Infrastructure.Email;

public class PostmarkOptions
{
    public const string SectionName = "Postmark";

    public string ApiKey { get; set; } = string.Empty;

    public string FromAddress { get; set; } = "notifications@devlog.example";
}
