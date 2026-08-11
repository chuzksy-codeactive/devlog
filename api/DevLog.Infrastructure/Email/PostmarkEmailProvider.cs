using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace DevLog.Infrastructure.Email;

public class PostmarkEmailProvider : IEmailProvider
{
    private readonly HttpClient _httpClient;
    private readonly PostmarkOptions _options;

    public PostmarkEmailProvider(HttpClient httpClient, IOptions<PostmarkOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public async Task SendAsync(string toEmail, string subject, string textBody, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            throw new InvalidOperationException("Postmark:ApiKey is missing. Configure a Postmark Server API token.");
        }

        if (string.IsNullOrWhiteSpace(_options.FromAddress))
        {
            throw new InvalidOperationException("Postmark:FromAddress is missing. Configure a verified sender signature.");
        }

        var request = new PostmarkSendRequest(_options.FromAddress, toEmail, subject, textBody);
        using var response = await _httpClient.PostAsJsonAsync("email", request, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        throw new HttpRequestException(
            $"Postmark send failed with status code {(int)response.StatusCode}: {responseBody}",
            null,
            response.StatusCode);
    }

    private record PostmarkSendRequest(
        [property: JsonPropertyName("From")] string From,
        [property: JsonPropertyName("To")] string To,
        [property: JsonPropertyName("Subject")] string Subject,
        [property: JsonPropertyName("TextBody")] string TextBody,
        [property: JsonPropertyName("MessageStream")] string MessageStream = "outbound");
}
