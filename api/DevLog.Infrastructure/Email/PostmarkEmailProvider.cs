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
        var request = new PostmarkSendRequest(_options.FromAddress, toEmail, subject, textBody);
        var response = await _httpClient.PostAsJsonAsync("email", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private record PostmarkSendRequest(
        [property: JsonPropertyName("From")] string From,
        [property: JsonPropertyName("To")] string To,
        [property: JsonPropertyName("Subject")] string Subject,
        [property: JsonPropertyName("TextBody")] string TextBody,
        [property: JsonPropertyName("MessageStream")] string MessageStream = "outbound");
}
