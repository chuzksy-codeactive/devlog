namespace DevLog.Infrastructure.Email;

// Bound from configuration section "Postmark".
public class PostmarkOptions
{
    public const string SectionName = "Postmark";

    public string ApiKey { get; set; } = string.Empty;

    // Must be a Sender Signature (or a domain) verified in the Postmark
    // dashboard — Postmark rejects the send outright otherwise, it doesn't
    // silently accept an unverified From address.
    public string FromAddress { get; set; } = "notifications@devlog.example";
}
