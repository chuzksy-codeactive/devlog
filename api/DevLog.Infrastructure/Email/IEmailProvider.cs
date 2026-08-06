namespace DevLog.Infrastructure.Email;

public interface IEmailProvider
{
    Task SendAsync(string toEmail, string subject, string textBody, CancellationToken cancellationToken = default);
}
