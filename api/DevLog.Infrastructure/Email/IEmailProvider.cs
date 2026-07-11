namespace DevLog.Infrastructure.Email;

// Deliberately its own small abstraction, not folded into ILlmProvider —
// different concern (transactional delivery, not generation) and a
// different vendor category entirely. Same "interface + swappable
// HttpClient-backed implementation" shape as ILlmProvider/IEmbeddingProvider
// though, for the same reason: if Postmark access ever gets blocked the way
// the Anthropic console key did, swapping to SendGrid should be a DI
// registration change, not a rewrite.
public interface IEmailProvider
{
    Task SendAsync(string toEmail, string subject, string textBody, CancellationToken cancellationToken = default);
}
